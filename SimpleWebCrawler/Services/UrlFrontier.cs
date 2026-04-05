using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using System.Threading.Channels;

namespace SimpleWebCrawler.Services;

public class UrlFrontier : IUrlFrontier
{
    private readonly Channel<Uri> _channel;
    private readonly HashSet<Uri> _seenUrls = [];
    private readonly Lock _lock = new();

    public UrlFrontier(CrawlerOptions options)
    {
        _channel = Channel.CreateBounded<Uri>(new BoundedChannelOptions(options.MaxChannelSize)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public async ValueTask<bool> TryEnqueueAsync(Uri url, CancellationToken cancellationToken)
    {
        bool isNew;
        lock (_lock)
        {
            isNew = _seenUrls.Add(url);
        }

        if (isNew)
        {
            await _channel.Writer.WriteAsync(url, cancellationToken);
        }

        return isNew;
    }

    public IAsyncEnumerable<Uri> DequeueAllAsync(CancellationToken cancellationToken)
        => _channel.Reader.ReadAllAsync(cancellationToken);

    public bool TryComplete()
        => _channel.Writer.TryComplete();
}