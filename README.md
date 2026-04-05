# Simple Web Crawler

A concurrent web crawler built in C# / .NET 10 that given a starting URL crawls all pages within a single subdomain.

## Features

- ✅ Crawls all pages on the same subdomain
- ✅ Prints each URL visited and links discovered on that page
- ✅ Separates internal and external links (doesn't crawl external links)
- ✅ Concurrent crawling with configurable parallelism
- ✅ Graceful cancellation (Ctrl+C)
- ✅ Duplicate URL detection
- ✅ URL normalization (fragments, trailing slashes, ports)
- ✅ Retry with exponential backoff
- ✅ Polite crawling with configurable delays

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Quick Start
Clone the repository OR unzip the provided source code, navigate to the project directory, and run the crawler with a starting URL:

```bash
cd C:\Projects\SimpleWebCrawler\SimpleWebCrawler

dotnet run -- https://crawlme.monzo.com/
```

Or from the solution root:

```bash
cd C:\Projects\SimpleWebCrawler

dotnet run --project SimpleWebCrawler -- https://crawlme.monzo.com/
```

## How It Works

1. **Start**: `Program.cs` accepts a URL, sets up DI, creates channels
2. **Enqueue**: Starting URL is added to `UrlFrontier`
3. **Workers**: N workers (default: 100) concurrently:
   - Dequeue URL from frontier
   - HTTP GET the page
   - Parse HTML with `LinkExtractor`
   - Categorize links as internal/external
   - Enqueue new internal links if they weren't seen before
   - Write result to output channel
4. **Output**: `ConsoleOutput` batches results for efficient printing
5. **Completion**: When `pendingCount` reaches 0, frontier completes, workers exit

App prints all links it crawls as well as links it detects on the page.
Example:
<img width="732" height="236" alt="image" src="https://github.com/user-attachments/assets/94cd8136-8133-45a7-827c-77168311c320" />


Upon completion (successful or failed) small run summary will be provided:
<img width="517" height="206" alt="image" src="https://github.com/user-attachments/assets/b5206dbc-41ef-433d-8ad9-01110c62a265" />



### Key Components

| Component | Responsibility |
|-----------|----------------|
| **CrawlerOrchestrator** | Manages concurrent workers, tracks in-flight work, coordinates completion |
| **UrlFrontier** | Thread-safe bounded channel + HashSet for O(1) duplicate detection |
| **LinkExtractor** | Parses HTML with HtmlAgilityPack, normalizes URLs, categorizes as internal/external |
| **ConsoleOutput** | Batches output to reduce console I/O overhead |

### Concurrency Model

- **Producer-Consumer pattern** using `System.Threading.Channels`
- **Multiple workers** dequeue URLs from `UrlFrontier`, process them, and enqueue discovered links
- **Pending count tracking** ensures proper completion (no premature shutdown)
- **Backpressure** via bounded channels prevents memory exhaustion

## Configuration

All settings are in `CrawlerOptions.cs`:

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxConcurrency` | 100 | Concurrent HTTP requests (balances speed vs. server load) |
| `MaxChannelSize` | 10,000 | URL queue capacity |
| `RequestTimeout` | 30s | HTTP request timeout |
| `MaxRetries` | 3 | Retry attempts with exponential backoff |
| `DelayBetweenRequests` | 1ms | Politeness delay |
| `ConsoleBatchSize` | 500 | Results batched before printing |

## Trade-offs & Design Decisions

### **BFS vs DFS Traversal**
- **Chose:** BFS (Breadth-First Search) using FIFO queue
- **Why:** Discovers pages closer to the starting URL first, distributes load evenly across site structure, more predictable crawl order
- **Trade-off:** Uses more memory than DFS (must hold entire frontier), but better for finding important pages early

### **Channel-based vs Task-based Concurrency**
- **Chose:** `System.Threading.Channels` for URL queue
- **Why:** Provides natural backpressure, efficient producer-consumer pattern, and clean completion semantics
- **Trade-off:** Slightly more complex than simple `Task.WhenAll`, but scales better

### **Bounded Channels**
- **Chose:** Bounded capacity for both URL queue and results
- **Why:** Prevents unbounded memory growth, applies natural backpressure
- **Trade-off:** Could block producers if consumers are slow (acceptable)

### In-Memory Duplicate Detection
- **Chose:** `HashSet<Uri>` + `Lock` over `ConcurrentDictionary`
- **Why:** 
  - .NET has no `ConcurrentHashSet` — would need `ConcurrentDictionary<Uri, byte>` (wastes memory)
  - `HashSet.Add()` with Lock makes the operation atomic
  - Lock held for nanoseconds (no I/O inside), using .NET 9's efficient `Lock` type
- **Trade-off:** Memory grows with URLs visited; for very large sites, would need probabilistic data structure (Bloom filter) or persistent storage

### **Batched Console Output**
- **Chose:** Buffer results and print in batches (default: 500 results per batch)
- **Why:** Console I/O is slow and can become a bottleneck; batching reduces system calls
- **Trade-off:** Slight delay before seeing output, but significantly improves throughput on large crawls

- ### **Single HttpClient with Connection Pooling**
- **Chose:** `IHttpClientFactory` with `SocketsHttpHandler`
- **Why:** Proper connection reuse, avoids socket exhaustion
- **Trade-off:** All requests share retry/timeout policies (appropriate for single-domain crawling)

## Running Tests

dotnet test

## Future Improvements

- **robots.txt support** - Parse and respect crawling rules
- **Persistent state** - Resume interrupted crawls (useful for large-scale crawling)
- **Bloom filter** - Memory-efficient duplicate detection for large data sets
- **Per-domain rate limiting** - Throttle requests per host independently
- **Distributed crawling** - Scale horizontally with multiple crawler instances



