using SimpleWebCrawler.Models;
using System.Text;
using System.Threading.Channels;

namespace SimpleWebCrawler.Helpers;

/// <summary>
/// Console output formatting for crawl results.
/// </summary>
public static class ConsoleOutput
{
    public static void PrintStartInfo(Uri startUrl, CrawlerOptions options)
    {
        Console.WriteLine("========================================");
        Console.WriteLine("       Simple Web Crawler v1.0");
        Console.WriteLine("========================================");
        Console.WriteLine($"Starting URL: {startUrl}");
        Console.WriteLine($"Concurrency:  {options.MaxConcurrency}");
        Console.WriteLine("Press Ctrl+C to cancel\n");
    }

    public static void PrintSummary(CrawlRunSummary stats, TimeSpan elapsed)
    {
        var pagesPerSecond = elapsed.TotalSeconds > 0
               ? stats.SuccessCount / elapsed.TotalSeconds
               : 0;

        Console.WriteLine("\n========================================");
        Console.WriteLine("              SUMMARY");
        Console.WriteLine("========================================");
        Console.WriteLine($"  Pages crawled:   {stats.SuccessCount}");
        Console.WriteLine($"  Failed:          {stats.FailedCount}");
        Console.WriteLine($"  Time elapsed:    {elapsed.TotalSeconds:F2}s");
        Console.WriteLine($"  Pages/second:    {pagesPerSecond:F1}");
        Console.WriteLine("========================================");
    }

    /// <summary>
    /// Reads from channel and prints results in batches.
    /// </summary>
    public static async Task PrintResultsBatchedAsync(
        ChannelReader<UrlCrawlResult> results,
        CrawlerOptions options,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        var count = 0;

        await foreach (var result in results.ReadAllAsync(cancellationToken))
        {
            AppendResult(sb, result);
            count++;

            if (count >= options.ConsoleBatchSize)
            {
                Console.Write(sb.ToString());
                sb.Clear();
                count = 0;
            }
        }

        if (sb.Length > 0)
        {
            Console.Write(sb.ToString());
        }
    }

    private static void AppendResult(StringBuilder sb, UrlCrawlResult result)
    {
        sb.AppendLine();
        sb.AppendLine($"[OK] Visited: {result.Url}");

        sb.AppendLine($"  Internal links found ({result.InternalLinks.Count}):");
        foreach (var link in result.InternalLinks)
        {
            sb.AppendLine($"    -> {link}");
        }

        if (result.ExternalLinks.Count > 0)
        {
            sb.AppendLine($"  External links found ({result.ExternalLinks.Count}):");
            foreach (var link in result.ExternalLinks)
            {
                sb.AppendLine($"    [EXT] {link}");
            }
        }
    }
}