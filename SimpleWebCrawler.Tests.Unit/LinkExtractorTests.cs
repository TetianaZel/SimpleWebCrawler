using SimpleWebCrawler.Services;

namespace SimpleWebCrawler.Tests.Unit;

public class LinkExtractorTests
{
    private readonly LinkExtractor _sut = new();
    private readonly Uri _baseUrl = new("https://example.com/");
    private readonly Uri _currentPage = new("https://example.com/page1");

    [Fact]
    public void ExtractLinks_WithInternalLinks_ReturnsInternalList()
    {
        var html = """
            <html><body>
                <a href="/page2">Page 2</a>
                <a href="https://example.com/page3">Page 3</a>
            </body></html>
            """;

        var (internalLinks, externalLinks) = _sut.ExtractLinks(html, _currentPage, _baseUrl);

        Assert.Equal(2, internalLinks.Count);
        Assert.Empty(externalLinks);
    }

    [Fact]
    public void ExtractLinks_WithExternalLinks_ReturnsExternalList()
    {
        var html = """<html><body><a href="https://other-site.com/page">External</a></body></html>""";

        var (internalLinks, externalLinks) = _sut.ExtractLinks(html, _currentPage, _baseUrl);

        Assert.Empty(internalLinks);
        Assert.Single(externalLinks);
        Assert.Equal("other-site.com", externalLinks[0].Host);
    }

    [Fact]
    public void ExtractLinks_WithMixedLinks_CategoriesCorrectly()
    {
        var html = """
            <html><body>
                <a href="/internal">Internal</a>
                <a href="https://external.com">External</a>
            </body></html>
            """;

        var (internalLinks, externalLinks) = _sut.ExtractLinks(html, _currentPage, _baseUrl);

        Assert.Single(internalLinks);
        Assert.Single(externalLinks);
    }

    [Theory]
    [InlineData("#section")]
    [InlineData("mailto:test@example.com")]
    [InlineData("javascript:void(0)")]
    [InlineData("tel:+1234567890")]
    public void ExtractLinks_WithNonHttpLinks_IgnoresThem(string href)
    {
        var html = $"""<html><body><a href="{href}">Link</a></body></html>""";

        var (internalLinks, externalLinks) = _sut.ExtractLinks(html, _currentPage, _baseUrl);

        Assert.Empty(internalLinks);
        Assert.Empty(externalLinks);
    }

    [Fact]
    public void ExtractLinks_WithDuplicateLinks_DeduplicatesOnPage()
    {
        var html = """
            <html><body>
                <a href="/page2">Link 1</a>
                <a href="/page2">Link 2</a>
                <a href="/page2">Link 3</a>
            </body></html>
            """;

        var (internalLinks, _) = _sut.ExtractLinks(html, _currentPage, _baseUrl);

        Assert.Single(internalLinks);
    }

    [Fact]
    public void ExtractLinks_WithEmptyHtml_ReturnsEmptyLists()
    {
        var (internalLinks, externalLinks) = _sut.ExtractLinks("", _currentPage, _baseUrl);

        Assert.Empty(internalLinks);
        Assert.Empty(externalLinks);
    }

    [Fact]
    public void ExtractLinks_WithRelativeLinks_ResolvesCorrectly()
    {
        var currentPage = new Uri("https://example.com/folder/page1");
        var html = """<html><body><a href="../other">Link</a></body></html>""";

        var (internalLinks, _) = _sut.ExtractLinks(html, currentPage, _baseUrl);

        Assert.Single(internalLinks);
        Assert.Equal("/other", internalLinks[0].AbsolutePath);
    }
}