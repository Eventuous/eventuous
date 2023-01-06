using System.Diagnostics.Tracing;
using Eventuous.Diagnostics.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.AspNetCore;

[PublicAPI]
public static class LoggingAppBuilderExtensions {
    /// <summary>
    /// Add Eventuous logging from internal event sources to the application logging
    /// </summary>
    /// <param name="host">Host builder</param>
    /// <param name="level">Event level, default is Verbose. Decrease the level to improve performance.</param>
    /// <param name="keywords">Event keywords, default is All</param>
    /// <returns></returns>
    public static IHost AddEventuousLogs(
        this IHost    host,
        EventLevel    level    = EventLevel.Verbose,
        EventKeywords keywords = EventKeywords.All
    ) {
        AddEventuousLogs(host.Services, level, keywords);
        return host;
    }

    /// <summary>
    /// Add Eventuous logging from internal event sources to the application logging
    /// </summary>
    /// <param name="provider">DI container, which has a logger factory registered</param>
    /// <param name="level">Event level, default is Verbose. Decrease the level to improve performance.</param>
    /// <param name="keywords">Event keywords, default is All</param>
    /// <returns></returns>
    public static void AddEventuousLogs(
        this IServiceProvider provider,
        EventLevel            level    = EventLevel.Verbose,
        EventKeywords         keywords = EventKeywords.All
    )
        => listener ??= new LoggingEventListener(provider.GetRequiredService<ILoggerFactory>(),
            level: level,
            keywords: keywords
        );

    static LoggingEventListener? listener;
}