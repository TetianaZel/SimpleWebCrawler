using HtmlAgilityPack;
using SimpleWebCrawler.Interfaces;

namespace SimpleWebCrawler.Services;

public class LinkExtractor : ILinkExtractor
{
    public (IReadOnlyList<Uri> Internal, IReadOnlyList<Uri> External) ExtractLinks(
        string html,
        Uri currentPageUrl,
        Uri baseUrl)
    {
        var internalLinks = new List<Uri>();
        var externalLinks = new List<Uri>();
        var seenOnPage = new HashSet<Uri>();

        foreach (var link in ParseLinksFromHtml(html))
        {
            var uri = TryCreateAbsoluteUri(currentPageUrl, link);

            if (uri == null || !seenOnPage.Add(uri))
                continue;

            if (IsSameDomain(baseUrl, uri))
                internalLinks.Add(uri);
            else
                externalLinks.Add(uri);
        }

        return (internalLinks, externalLinks);
    }

    private static IEnumerable<string> ParseLinksFromHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        var links = new List<string>();

        // Anchor tags
        var anchorNodes = document.DocumentNode.SelectNodes("//a[@href]");
        if (anchorNodes != null)
            links.AddRange(anchorNodes.Select(n => n.GetAttributeValue("href", "")));

        // Area tags (image maps)
        var areaNodes = document.DocumentNode.SelectNodes("//area[@href]");
        if (areaNodes != null)
            links.AddRange(areaNodes.Select(n => n.GetAttributeValue("href", "")));

        // Frames/iframes
        var frameNodes = document.DocumentNode.SelectNodes("//frame[@src] | //iframe[@src]");
        if (frameNodes != null)
            links.AddRange(frameNodes.Select(n => n.GetAttributeValue("src", "")));

        return links.Where(href => !string.IsNullOrWhiteSpace(href));
    }

    private static bool IsSameDomain(Uri baseUrl, Uri candidateUrl)
    {
        return string.Equals(baseUrl.Host, candidateUrl.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static Uri? TryCreateAbsoluteUri(Uri baseUrl, string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return null;

        if (IsNonHttpLink(link))
            return null;

        if (!Uri.TryCreate(baseUrl, link, out var absoluteUri))
            return null;

        if (absoluteUri.Scheme != Uri.UriSchemeHttp &&
            absoluteUri.Scheme != Uri.UriSchemeHttps)
            return null;

        return NormalizeUri(absoluteUri);
    }

    private static bool IsNonHttpLink(string link)
    {
        ReadOnlySpan<char> linkSpan = link.AsSpan().Trim();

        return linkSpan.StartsWith("#", StringComparison.Ordinal) ||
               linkSpan.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
               linkSpan.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase) ||
               linkSpan.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
               linkSpan.StartsWith("data:", StringComparison.OrdinalIgnoreCase);
    }

    private static Uri NormalizeUri(Uri uri)
    {
        var builder = new UriBuilder(uri)
        {
            Fragment = string.Empty,
            Host = uri.Host.ToLowerInvariant()
        };

        if ((uri.Scheme == Uri.UriSchemeHttp && uri.Port == 80) ||
            (uri.Scheme == Uri.UriSchemeHttps && uri.Port == 443))
        {
            builder.Port = -1;
        }

        if (builder.Path.Length > 1 && builder.Path.EndsWith('/'))
        {
            builder.Path = builder.Path.TrimEnd('/');
        }

        return builder.Uri;
    }
}