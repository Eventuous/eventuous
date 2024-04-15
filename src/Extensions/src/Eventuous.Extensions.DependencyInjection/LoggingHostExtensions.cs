// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics.Logging;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;

using DependencyInjection;
using Logging;

[PublicAPI]
public static class LoggingAppBuilderExtensions {
    /// <summary>
    /// Add Eventuous logging from internal event sources to the application logging
    /// </summary>
    /// <param name="host">Host builder</param>
    /// <param name="level">Event level, default is Verbose. Decrease the level to improve performance.</param>
    /// <param name="keywords">Event keywords, default is All</param>
    /// <returns></returns>
    public static IHost UseEventuousLogs(this IHost host, EventLevel level = EventLevel.Verbose, EventKeywords keywords = EventKeywords.All) {
        AddEventuousLogs(host.Services, level, keywords);

        return host;
    }

    /// <summary>
    /// Adds the Eventuous logging from internal event sources to the application logging.
    /// You'd not normally call this method directly, but use <see cref="UseEventuousLogs"/>
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="level"></param>
    /// <param name="keywords"></param>
    public static void AddEventuousLogs(this IServiceProvider provider, EventLevel level = EventLevel.Verbose, EventKeywords keywords = EventKeywords.All) {
        var factory = provider.GetService<ILoggerFactory>();

        if (factory != null)
            listener ??= new LoggingEventListener(factory, level: level, keywords: keywords);
    }

    static LoggingEventListener? listener;
}
