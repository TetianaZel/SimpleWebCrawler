using Moq;
using Moq.Protected;
using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using SimpleWebCrawler.Services;
using System.Net;
using System.Threading.Channels;

namespace SimpleWebCrawler.Tests.Unit;

public class CrawlerOrchestratorTests
{
    private readonly Mock<IUrlFrontier> _frontierMock = new();
    private readonly Mock<ILinkExtractor> _linkExtractorMock = new();
    private readonly Mock<HttpMessageHandler> _httpHandlerMock = new();
    private readonly CrawlerOptions _options = new() { MaxConcurrency = 1 };
    private readonly CrawlRunSummary _summary = new();

    [Fact]
    public async Task CrawlAsync_SuccessfulPage_IncrementsSuccessCount()
    {
        var startUrl = new Uri("https://example.com/");
        SetupHttpResponse(startUrl, "<html><body>Hello</body></html>");
        SetupFrontierWithSingleUrl(startUrl);
        SetupLinkExtractor([], []);

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await orchestrator.CrawlAsync(startUrl, channel.Writer, CancellationToken.None);

        Assert.Equal(1, _summary.SuccessCount);
        Assert.Equal(0, _summary.FailedCount);
    }

    [Fact]
    public async Task CrawlAsync_FailedPage_IncrementsFailedCount()
    {
        var startUrl = new Uri("https://example.com/");
        SetupHttpError(startUrl, HttpStatusCode.InternalServerError);
        SetupFrontierWithSingleUrl(startUrl);

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await orchestrator.CrawlAsync(startUrl, channel.Writer, CancellationToken.None);

        Assert.Equal(0, _summary.SuccessCount);
        Assert.Equal(1, _summary.FailedCount);
    }

    [Fact]
    public async Task CrawlAsync_WritesResultsToChannel()
    {
        var startUrl = new Uri("https://example.com/");
        SetupHttpResponse(startUrl, "<html><body>Hello</body></html>");
        SetupFrontierWithSingleUrl(startUrl);
        SetupLinkExtractor([], []);

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await orchestrator.CrawlAsync(startUrl, channel.Writer, CancellationToken.None);

        Assert.True(channel.Reader.TryRead(out var result));
        Assert.Equal(startUrl, result.Url);
    }

    [Fact]
    public async Task CrawlAsync_CompletesChannelWhenDone()
    {
        var startUrl = new Uri("https://example.com/");
        SetupHttpResponse(startUrl, "<html></html>");
        SetupFrontierWithSingleUrl(startUrl);
        SetupLinkExtractor([], []);

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await orchestrator.CrawlAsync(startUrl, channel.Writer, CancellationToken.None);

        // Verify the channel writer was completed (TryComplete called in finally block)
        Assert.False(channel.Writer.TryWrite(new UrlCrawlResult(startUrl, [], [])));
    }

    [Fact]
    public async Task CrawlAsync_EnqueuesDiscoveredInternalLinks()
    {
        var startUrl = new Uri("https://example.com/");
        var discoveredUrl = new Uri("https://example.com/page2");

        SetupHttpResponse(startUrl, "<html></html>");
        SetupFrontierWithSingleUrl(startUrl);
        SetupLinkExtractor([discoveredUrl], []);

        _frontierMock
            .Setup(f => f.TryEnqueueAsync(discoveredUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await orchestrator.CrawlAsync(startUrl, channel.Writer, CancellationToken.None);

        _frontierMock.Verify(
            f => f.TryEnqueueAsync(discoveredUrl, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetSummary_ReturnsCrawlStatistics()
    {
        var orchestrator = CreateOrchestrator();

        var summary = orchestrator.GetSummary();

        Assert.Same(_summary, summary);
    }

    [Fact]
    public async Task CrawlAsync_WhenCancelled_StopsGracefully()
    {
        var startUrl = new Uri("https://example.com/");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Setup frontier to throw when token is cancelled
        _frontierMock
            .Setup(f => f.TryEnqueueAsync(It.IsAny<Uri>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var orchestrator = CreateOrchestrator();
        var channel = Channel.CreateUnbounded<UrlCrawlResult>();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => orchestrator.CrawlAsync(startUrl, channel.Writer, cts.Token));
    }

    private CrawlerOrchestrator CreateOrchestrator()
    {
        return new CrawlerOrchestrator(
            _frontierMock.Object,
            new HttpClient(_httpHandlerMock.Object),
            _linkExtractorMock.Object,
            _options,
            _summary);
    }

    private void SetupHttpResponse(Uri url, string htmlContent)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(htmlContent)
        };
        response.Content.Headers.ContentType = new("text/html");

        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }

    private void SetupHttpError(Uri url, HttpStatusCode statusCode)
    {
        _httpHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(r => r.RequestUri == url),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(statusCode));
    }

    private void SetupFrontierWithSingleUrl(Uri url)
    {
        _frontierMock
            .Setup(f => f.TryEnqueueAsync(url, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _frontierMock
            .Setup(f => f.DequeueAllAsync(It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(url));

        _frontierMock.Setup(f => f.TryComplete()).Returns(true);
    }

    private void SetupLinkExtractor(List<Uri> internalLinks, List<Uri> externalLinks)
    {
        _linkExtractorMock
            .Setup(l => l.ExtractLinks(It.IsAny<string>(), It.IsAny<Uri>(), It.IsAny<Uri>()))
            .Returns((internalLinks.AsReadOnly(), externalLinks.AsReadOnly()));
    }

    private static async IAsyncEnumerable<Uri> ToAsyncEnumerable(Uri url)
    {
        yield return url;
        await Task.CompletedTask;
    }
}