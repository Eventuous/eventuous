// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;

using DependencyInjection;
using Logging;

[PublicAPI]
public static class LoggingServiceProviderExtensions {
    /// <summary>
    /// Adds the Eventuous logging from internal event sources to the application logging.
    /// You'd not normally call this method directly, but use <code>UseEventuousLogs</code> from Eventuous.Extensions.AspNetCore/>
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="level"></param>
    /// <param name="keywords"></param>
    public static void AddEventuousLogs(this IServiceProvider provider, EventLevel level = EventLevel.Verbose, EventKeywords keywords = EventKeywords.All) {
        var factory = provider.GetService<ILoggerFactory>();

        factory.AddEventuousLogs(level, keywords);
    }
}
