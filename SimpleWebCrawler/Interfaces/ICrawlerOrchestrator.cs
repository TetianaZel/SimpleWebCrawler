using SimpleWebCrawler.Models;
using System.Threading.Channels;

namespace SimpleWebCrawler.Interfaces;

/// <summary>
/// Orchestrates the crawling process: manages workers, tracks completion.
/// Crawls starting from the given URL, writing results to the provided channel.
/// Completes the channel when done.
/// </summary>
public interface ICrawlerOrchestrator
{
    Task CrawlAsync(Uri startUrl, ChannelWriter<UrlCrawlResult> results, CancellationToken cancellationToken);

    CrawlRunSummary GetSummary();
}