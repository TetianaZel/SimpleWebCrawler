namespace SimpleWebCrawler.Models;

/// <summary>
/// Represents the result of crawling a single URL.
/// </summary>
/// <param name="Url">The URL that was crawled.</param>
/// <param name="InternalLinks">Links on same subdomain </param>
/// <param name="ExternalLinks">Links to other domains </param>
public record UrlCrawlResult(Uri Url, IReadOnlyList<Uri> InternalLinks, IReadOnlyList<Uri> ExternalLinks);