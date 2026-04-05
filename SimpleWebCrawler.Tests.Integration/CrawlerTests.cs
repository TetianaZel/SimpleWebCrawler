using Microsoft.Extensions.DependencyInjection;
using SimpleWebCrawler.Extensions;
using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using System.Threading.Channels;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace SimpleWebCrawler.Tests.Integration;

public class CrawlerTests : IDisposable
{
    private readonly WireMockServer _server;
    private readonly ServiceProvider _serviceProvider;
    private readonly Uri _baseUrl;

    public CrawlerTests()
    {
        _server = WireMockServer.Start();
        _baseUrl = new Uri($"{_server.Url}/");

        var options = new CrawlerOptions
        {
            MaxConcurrency = 2,
            MaxRetries = 0,  // Disable retries for faster tests
            DelayBetweenRequests = TimeSpan.Zero,
            RequestTimeout = TimeSpan.FromSeconds(10),
            PooledConnectionLifetime = TimeSpan.FromSeconds(1)  // Short lifetime for tests
        };

        var services = new ServiceCollection();
        services.AddCrawlerServices(options);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _server.Stop();
        _server.Dispose();
    }

    [Fact]
    public async Task CrawlAsync_SinglePage_ReturnsResult()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body><a href='/page1'>Link</a></body></html>"));

        _server.Given(Request.Create().WithPath("/page1"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body>Page 1</body></html>"));

        var crawler = _serviceProvider.GetRequiredService<ICrawlerOrchestrator>();
        var channel = Channel.CreateBounded<UrlCrawlResult>(100);

        // Act
        await crawler.CrawlAsync(_baseUrl, channel.Writer, CancellationToken.None);

        var results = await ReadAllResultsAsync(channel.Reader);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Url.AbsolutePath == "/");
        Assert.Contains(results, r => r.Url.AbsolutePath == "/page1");
    }

    [Fact]
    public async Task CrawlAsync_WithExternalLinks_DoesNotCrawlExternal()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><body><a href='https://external.com'>External</a></body></html>"));

        var crawler = _serviceProvider.GetRequiredService<ICrawlerOrchestrator>();
        var channel = Channel.CreateBounded<UrlCrawlResult>(100);

        // Act
        await crawler.CrawlAsync(_baseUrl, channel.Writer, CancellationToken.None);

        var results = await ReadAllResultsAsync(channel.Reader);

        // Assert
        Assert.Single(results);
        var result = results[0];
        Assert.Empty(result.InternalLinks);
        Assert.Single(result.ExternalLinks);
    }

    [Fact]
    public async Task CrawlAsync_MultiplePagesWithLinks_CrawlsAllInternalPages()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><a href='/a'>A</a><a href='/b'>B</a></html>"));

        _server.Given(Request.Create().WithPath("/a"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><a href='/c'>C</a></html>"));

        _server.Given(Request.Create().WithPath("/b"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html>B Page</html>"));

        _server.Given(Request.Create().WithPath("/c"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html>C Page</html>"));

        var crawler = _serviceProvider.GetRequiredService<ICrawlerOrchestrator>();
        var channel = Channel.CreateBounded<UrlCrawlResult>(100);

        // Act
        await crawler.CrawlAsync(_baseUrl, channel.Writer, CancellationToken.None);

        var results = await ReadAllResultsAsync(channel.Reader);

        // Assert
        Assert.Equal(4, results.Count);
        var summary = crawler.GetSummary();
        Assert.Equal(4, summary.SuccessCount);
        Assert.Equal(0, summary.FailedCount);
    }

    [Fact]
    public async Task CrawlAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        _server.Given(Request.Create().WithPath("/"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><a href='/slow'>Slow</a></html>"));

        _server.Given(Request.Create().WithPath("/slow"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithDelay(TimeSpan.FromSeconds(30))
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html>Slow page</html>"));

        var crawler = _serviceProvider.GetRequiredService<ICrawlerOrchestrator>();
        var channel = Channel.CreateBounded<UrlCrawlResult>(100);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => crawler.CrawlAsync(_baseUrl, channel.Writer, cts.Token));
    }

    [Fact]
    public async Task CrawlAsync_CircularLinks_DoesNotCrawlDuplicates()
    {
        // Arrange - create circular reference
        _server.Given(Request.Create().WithPath("/"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><a href='/page1'>Page1</a></html>"));

        _server.Given(Request.Create().WithPath("/page1"))
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "text/html; charset=utf-8")
                .WithBody("<html><a href='/'>Back to home</a><a href='/page1'>Self</a></html>"));

        var crawler = _serviceProvider.GetRequiredService<ICrawlerOrchestrator>();
        var channel = Channel.CreateBounded<UrlCrawlResult>(100);

        // Act
        await crawler.CrawlAsync(_baseUrl, channel.Writer, CancellationToken.None);

        var results = await ReadAllResultsAsync(channel.Reader);

        // Assert - should only crawl each URL once
        Assert.Equal(2, results.Count);
    }

    private static async Task<List<UrlCrawlResult>> ReadAllResultsAsync(ChannelReader<UrlCrawlResult> reader)
    {
        var results = new List<UrlCrawlResult>();
        await foreach (var result in reader.ReadAllAsync())
        {
            results.Add(result);
        }
        return results;
    }
}