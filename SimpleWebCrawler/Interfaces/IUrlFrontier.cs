namespace SimpleWebCrawler.Interfaces;

/// <summary>
/// Manages URL discovery: tracks seen URLs and queues work.
/// Combines queue management with duplicate detection.
/// </summary>
public interface IUrlFrontier
{
    /// <summary>
    /// Adds URL to queue if not already seen. Returns true if enqueued.
    /// </summary>
    ValueTask<bool> TryEnqueueAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Dequeues all URLs as they become available.
    /// </summary>
    IAsyncEnumerable<Uri> DequeueAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marks the queue as complete (no more items will be added).
    /// </summary>
    bool TryComplete();
}