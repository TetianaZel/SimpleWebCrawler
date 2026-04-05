using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using System.Threading.Channels;

namespace SimpleWebCrawler.Services;

public class CrawlerOrchestrator : ICrawlerOrchestrator
{
    private readonly IUrlFrontier _frontier;
    private readonly HttpClient _httpClient;
    private readonly ILinkExtractor _linkExtractor;
    private readonly CrawlerOptions _options;
    private readonly CrawlRunSummary _statistics;

    private int _pendingCount;

    public CrawlerOrchestrator(
        IUrlFrontier frontier,
        HttpClient httpClient,
        ILinkExtractor linkExtractor,
        CrawlerOptions options,
        CrawlRunSummary statistics)
    {
        _frontier = frontier;
        _httpClient = httpClient;
        _linkExtractor = linkExtractor;
        _options = options;
        _statistics = statistics;
    }

    public CrawlRunSummary GetSummary() => _statistics;

    public async Task CrawlAsync(
        Uri startUrl,
        ChannelWriter<UrlCrawlResult> urlResults,
        CancellationToken cancellationToken)
    {
        try
        {
            Interlocked.Increment(ref _pendingCount);
            await _frontier.TryEnqueueAsync(startUrl, cancellationToken);

            var workers = Enumerable
                .Range(0, _options.MaxConcurrency)
                .Select(_ => RunWorkerAsync(startUrl, urlResults, cancellationToken))
                .ToArray();

            await Task.WhenAll(workers);
        }
        finally
        {
            urlResults.TryComplete();
        }
    }

    private async Task RunWorkerAsync(Uri baseUrl, ChannelWriter<UrlCrawlResult> urlResults, CancellationToken cancellationToken)
    {
        await foreach (var url in _frontier.DequeueAllAsync(cancellationToken))
        {
            try
            {
                await ProcessUrlAsync(url, baseUrl, urlResults, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Console.WriteLine($"  [ERROR] {url}: {ex.Message}");
                _statistics.IncrementFailed();
            }
            finally
            {
                if (Interlocked.Decrement(ref _pendingCount) == 0)
                {
                    _frontier.TryComplete();
                }
            }

            if (_options.DelayBetweenRequests > TimeSpan.Zero)
            {
                await Task.Delay(_options.DelayBetweenRequests, cancellationToken);
            }
        }
    }

    private async Task ProcessUrlAsync(Uri currentUrl, Uri baseUrl, ChannelWriter<UrlCrawlResult> results, CancellationToken cancellationToken)
    {
        var html = await GetHtmlAsync(currentUrl, cancellationToken);

        if (html == null)
        {
            Console.WriteLine($"  [SKIP] Could not fetch: {currentUrl}");
            _statistics.IncrementFailed();
            return;
        }

        var (internalLinks, externalLinks) = _linkExtractor.ExtractLinks(html, currentUrl, baseUrl);
        var result = new UrlCrawlResult(currentUrl, internalLinks, externalLinks);

        await results.WriteAsync(result, cancellationToken);
        _statistics.IncrementSuccess();

        foreach (var link in internalLinks)
        {
            if (await _frontier.TryEnqueueAsync(link, cancellationToken))
            {
                Interlocked.Increment(ref _pendingCount);
            }
        }
    }

    private async Task<string?> GetHtmlAsync(Uri url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            return response.IsSuccessStatusCode
                && response.Content.Headers.ContentType?.MediaType?.Contains("text/html") == true
                ? await response.Content.ReadAsStringAsync(cancellationToken)
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        // Timeout - not user cancellation
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}