using Microsoft.Extensions.DependencyInjection;
using SimpleWebCrawler.Extensions;
using SimpleWebCrawler.Helpers;
using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using System.Diagnostics;
using System.Threading.Channels;

await RunCrawlerAsync(args);

static async Task RunCrawlerAsync(string[] args)
{
    if (args.Length == 0 || !Uri.TryCreate(args[0], UriKind.Absolute, out var startUrl))
    {
        Console.WriteLine("Usage: dotnet run -- <url>");
        Console.WriteLine("Example: dotnet run -- https://crawlme.monzo.com/");
        return;
    }

    var options = new CrawlerOptions();

    ConsoleOutput.PrintStartInfo(startUrl, options);

    using var services = new ServiceCollection()
        .AddCrawlerServices(options)
        .BuildServiceProvider();

    var crawler = services.GetRequiredService<ICrawlerOrchestrator>();
    using var cts = CreateCancellationTokenSource();

    var resultsChannel = Channel.CreateBounded<UrlCrawlResult>(
        new BoundedChannelOptions(options.UrlResultChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    var stopwatch = Stopwatch.StartNew();

    try
    {
        // Run crawling and printing concurrently
        var writerTask = crawler.CrawlAsync(startUrl, resultsChannel.Writer, cts.Token);
        var readerTask = ConsoleOutput.PrintResultsBatchedAsync(resultsChannel.Reader, options, cts.Token);

        // Await both - exceptions from either will propagate
        await Task.WhenAll(writerTask, readerTask);
    }
    catch (OperationCanceledException)
    {
        Console.WriteLine("\n[!] Crawling cancelled.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[!] Error: {ex.Message}");
    }
    finally
    {
        stopwatch.Stop();
        ConsoleOutput.PrintSummary(crawler.GetSummary(), stopwatch.Elapsed);
    }
}

static CancellationTokenSource CreateCancellationTokenSource()
{
    var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        cts.Cancel();
    };
    return cts;
}
