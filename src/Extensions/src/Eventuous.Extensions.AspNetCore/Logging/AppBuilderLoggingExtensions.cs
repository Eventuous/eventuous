// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Microsoft.AspNetCore.Builder;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.Hosting;


[PublicAPI]
public static class AppBuilderLoggingExtensions {
    /// <summary>
    /// Add Eventuous logging from internal event sources to the application logging
    /// </summary>
    /// <param name="host">Host builder</param>
    /// <param name="level">Event level, default is Verbose. Decrease the level to improve performance.</param>
    /// <param name="keywords">Event keywords, default is All</param>
    /// <returns></returns>
    public static IApplicationBuilder UseEventuousLogs(this IApplicationBuilder host, EventLevel level = EventLevel.Verbose, EventKeywords keywords = EventKeywords.All) {
        host.ApplicationServices.AddEventuousLogs(level, keywords);

        return host;
    }
    
}
