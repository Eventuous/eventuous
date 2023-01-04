// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Diagnostics;

[EventSource(Name = DiagnosticName.BaseName)]
class EventuousEventSource : EventSource {
    public static readonly EventuousEventSource Log = new();

    const int WarnId = 101;

    [Event(WarnId, Message = "{0} {1} {2}", Level = EventLevel.Warning)]
    public void Warn(string message, string? arg1 = null, string? arg2 = null) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All)) WriteEvent(WarnId, message, arg1, arg2);
    }
}