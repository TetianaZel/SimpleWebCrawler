namespace SimpleWebCrawler.Models;

/// <summary>
/// Statistics collected during a crawl session.
/// </summary>
public class CrawlRunSummary
{
    private int _successCount;
    private int _failedCount;

    public int SuccessCount => _successCount;
    public int FailedCount => _failedCount;
    public int TotalCount => _successCount + _failedCount;

    public void IncrementSuccess() => Interlocked.Increment(ref _successCount);
    public void IncrementFailed() => Interlocked.Increment(ref _failedCount);
}