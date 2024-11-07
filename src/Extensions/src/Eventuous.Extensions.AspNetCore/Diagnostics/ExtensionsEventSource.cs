// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

namespace Eventuous.Extensions.AspNetCore.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}.aspnetcore")]
public class ExtensionsEventSource : EventSource {
    const int HttpEndpointRegisteredId = 1;

    public static readonly ExtensionsEventSource Log = new();

    [NonEvent]
    public void HttpEndpointRegistered<T>(string route) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All))
            HttpEndpointRegistered(typeof(T).Name, route);
    }

    [Event(HttpEndpointRegisteredId, Message = "Http endpoint registered for {0} at {1}", Level = EventLevel.Verbose)]
    void HttpEndpointRegistered(string type, string route) => WriteEvent(HttpEndpointRegisteredId, type, route);
}
