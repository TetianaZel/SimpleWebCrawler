using SimpleWebCrawler.Models;
using SimpleWebCrawler.Services;

namespace SimpleWebCrawler.Tests.Unit;

public class UrlFrontierTests
{
    private readonly CrawlerOptions _options = new() { MaxChannelSize = 100 };
    private readonly CancellationToken _cancellationToken = CancellationToken.None;

    [Fact]
    public async Task TryEnqueueAsync_NewUrl_ReturnsTrue()
    {
        var frontier = new UrlFrontier(_options);
        var url = new Uri("https://example.com/page1");

        var result = await frontier.TryEnqueueAsync(url, _cancellationToken);

        Assert.True(result);
    }

    [Fact]
    public async Task TryEnqueueAsync_DuplicateUrl_ReturnsFalse()
    {
        var frontier = new UrlFrontier(_options);
        var url = new Uri("https://example.com/page1");

        await frontier.TryEnqueueAsync(url, _cancellationToken);
        var result = await frontier.TryEnqueueAsync(url, _cancellationToken);

        Assert.False(result);
    }

    [Fact]
    public async Task DequeueAllAsync_ReturnsEnqueuedUrls()
    {
        var frontier = new UrlFrontier(_options);
        var url1 = new Uri("https://example.com/page1");
        var url2 = new Uri("https://example.com/page2");

        await frontier.TryEnqueueAsync(url1, _cancellationToken);
        await frontier.TryEnqueueAsync(url2, _cancellationToken);
        frontier.TryComplete();

        var urls = new List<Uri>();
        await foreach (var url in frontier.DequeueAllAsync(_cancellationToken))
        {
            urls.Add(url);
        }

        Assert.Equal(2, urls.Count);
        Assert.Contains(url1, urls);
        Assert.Contains(url2, urls);
    }

    [Fact]
    public async Task TryComplete_StopsDequeueEnumeration()
    {
        var frontier = new UrlFrontier(_options);
        await frontier.TryEnqueueAsync(new Uri("https://example.com"), _cancellationToken);

        frontier.TryComplete();

        var count = 0;
        await foreach (var _ in frontier.DequeueAllAsync(_cancellationToken))
        {
            count++;
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TryEnqueueAsync_IsThreadSafe()
    {
        var frontier = new UrlFrontier(_options);
        var urls = Enumerable.Range(0, 100)
            .Select(i => new Uri($"https://example.com/page{i}"))
            .ToList();

        var results = await Task.WhenAll(
            urls.Select(url => frontier.TryEnqueueAsync(url, _cancellationToken).AsTask()));

        Assert.All(results, r => Assert.True(r));
    }
}