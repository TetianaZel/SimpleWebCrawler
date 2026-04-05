using Microsoft.Extensions.DependencyInjection;
using Polly;
using SimpleWebCrawler.Interfaces;
using SimpleWebCrawler.Models;
using SimpleWebCrawler.Services;

namespace SimpleWebCrawler.Extensions;

/// <summary>
/// Dependency Injection registration for crawler services.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCrawlerServices(
        this IServiceCollection services,
        CrawlerOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<CrawlRunSummary>();
        services.AddSingleton<IUrlFrontier, UrlFrontier>();

        services.AddHttpClient<ICrawlerOrchestrator, CrawlerOrchestrator>(client =>
        {
            client.Timeout = options.RequestTimeout;
        })
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            MaxConnectionsPerServer = options.MaxConcurrency,
            PooledConnectionLifetime = options.PooledConnectionLifetime
        })
        .AddTransientHttpErrorPolicy(policy =>
            policy.WaitAndRetryAsync(
                options.MaxRetries,
                attempt => TimeSpan.FromMilliseconds(
                    Math.Pow(2, attempt) * options.RetryBaseDelay.TotalMilliseconds)));

        services.AddTransient<ILinkExtractor, LinkExtractor>();

        return services;
    }
}