namespace SimpleWebCrawler.Models;

/// <summary>
/// Centralized configuration for the web crawler.
/// </summary>
public record CrawlerOptions
{
    /// <summary>
    /// Maximum number of concurrent HTTP requests.
    /// </summary>
    public int MaxConcurrency { get; init; } = 100;

    /// <summary>
    /// Maximum number of URLs waiting to be processed.
    /// </summary>
    public int MaxChannelSize { get; init; } = 10000;

    /// <summary>
    /// Maximum results buffered before backpressure is applied.
    /// </summary>
    public int UrlResultChannelCapacity { get; init; } = 10000;

    /// <summary>
    /// Timeout for individual HTTP requests.
    /// </summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Number of retry attempts for failed requests.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff.
    /// </summary>
    public TimeSpan RetryBaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// How long pooled connections stay alive.
    /// </summary>
    public TimeSpan PooledConnectionLifetime { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Delay between requests to be polite to the server.
    /// </summary>
    public TimeSpan DelayBetweenRequests { get; init; } = TimeSpan.FromMilliseconds(1);

    /// <summary>
    /// Number of results to batch before writing to console.
    /// </summary>
    public int ConsoleBatchSize { get; init; } = 500;
}