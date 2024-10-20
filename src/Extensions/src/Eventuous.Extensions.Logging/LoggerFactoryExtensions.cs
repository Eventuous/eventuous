// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics.Logging;
using Microsoft.Extensions.Logging;

namespace Eventuous.Extensions.Logging;

public static class LoggerFactoryExtensions {
    /// <summary>
    /// Adds the Eventuous logging from internal event sources to the application logging.
    /// Use it only if you are not building an ASP.NET Core application, otherwise use <code>UseEventuousLogs</code> from Eventuous.Extensions.AspNetCore
    /// </summary>
    /// <param name="factory">Logger factory instance</param>
    /// <param name="level"></param>
    /// <param name="keywords"></param>
    public static void AddEventuousLogs(this ILoggerFactory? factory, EventLevel level = EventLevel.Verbose, EventKeywords keywords = EventKeywords.All) {
        if (factory != null) listener ??= new(factory, level: level, keywords: keywords);
    }

    static LoggingEventListener? listener;
}
