namespace SimpleWebCrawler.Interfaces;

/// <summary>
/// Extracts links from HTML and processes them (validates, normalizes, categorizes).
/// </summary>
public interface ILinkExtractor
{
    (IReadOnlyList<Uri> Internal, IReadOnlyList<Uri> External) ExtractLinks(
        string html,
        Uri currentPageUrl,
        Uri baseUrl);
}