// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Subscriptions.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}-subscription")]
public class SubscriptionsEventSource : EventSource {
    public static readonly SubscriptionsEventSource Log = new();

    const int MetricCollectionFailedId   = 1;
    const int MessageTypeNotRegisteredId = 2;

    [NonEvent]
    public void MetricCollectionFailed(string metric, Exception exception)
        => MetricCollectionFailed(metric, exception.ToString());

    [NonEvent]
    public void MessageTypeNotRegistered<T>() => MessageTypeNotRegistered(typeof(T).Name);

    [Event(MetricCollectionFailedId, Message = "Failed to collect metric {0}: {1}", Level = EventLevel.Warning)]
    public void MetricCollectionFailed(string metric, string exception)
        => WriteEvent(MetricCollectionFailedId, metric, exception);

    [Event(MessageTypeNotRegisteredId, Message = "Message type {0} is not registered", Level = EventLevel.Warning)]
    public void MessageTypeNotRegistered(string messageType)
        => WriteEvent(MessageTypeNotRegisteredId, messageType);
}