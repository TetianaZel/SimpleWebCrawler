using SimpleWebCrawler.Models;

namespace SimpleWebCrawler.Tests.Unit;

public class CrawlRunSummaryTests
{
    [Fact]
    public void NewSummary_HasZeroCounts()
    {
        var summary = new CrawlRunSummary();

        Assert.Equal(0, summary.SuccessCount);
        Assert.Equal(0, summary.FailedCount);
        Assert.Equal(0, summary.TotalCount);
    }

    [Fact]
    public void IncrementSuccess_IncrementsSuccessCount()
    {
        var summary = new CrawlRunSummary();

        summary.IncrementSuccess();
        summary.IncrementSuccess();

        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(2, summary.TotalCount);
    }

    [Fact]
    public void IncrementFailed_IncrementsFailedCount()
    {
        var summary = new CrawlRunSummary();

        summary.IncrementFailed();

        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(1, summary.TotalCount);
    }

    [Fact]
    public void TotalCount_IsSumOfSuccessAndFailed()
    {
        var summary = new CrawlRunSummary();

        summary.IncrementSuccess();
        summary.IncrementSuccess();
        summary.IncrementFailed();

        Assert.Equal(2, summary.SuccessCount);
        Assert.Equal(1, summary.FailedCount);
        Assert.Equal(3, summary.TotalCount);
    }

    [Fact]
    public void Increments_AreThreadSafe()
    {
        var summary = new CrawlRunSummary();
        var iterations = 10000;

        Parallel.For(0, iterations, _ => summary.IncrementSuccess());
        Parallel.For(0, iterations, _ => summary.IncrementFailed());

        Assert.Equal(iterations, summary.SuccessCount);
        Assert.Equal(iterations, summary.FailedCount);
    }
}