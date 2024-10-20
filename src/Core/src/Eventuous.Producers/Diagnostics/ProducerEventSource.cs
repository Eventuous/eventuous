// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;
using Eventuous.Diagnostics;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Producers.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}.producer")]
public class ProducerEventSource<T> : EventSource where T : class {
    public static readonly ProducerEventSource<T> Log = new();
    
    static readonly string ProducerName = typeof(T).Name;

    const int ProduceAcknowledgedId    = 1;
    const int ProduceNotAcknowledgedId = 2;

    [NonEvent]
    public void ProduceAcknowledged(ProducedMessage message) {
        if (IsEnabled(EventLevel.Verbose, EventKeywords.All)) {
            ProduceAcknowledged(ProducerName, message.GetType().Name);
        }
    }

    [NonEvent]
    public void ProduceNotAcknowledged(ProducedMessage message, string error, Exception? e) {
        if (!IsEnabled(EventLevel.Verbose, EventKeywords.All)) return;

        var errorMessage = $"{error} {e?.Message}";
        ProduceNotAcknowledged(ProducerName, message.GetType().Name, errorMessage);
    }

    [Event(ProduceAcknowledgedId, Level = EventLevel.Verbose, Message = "[{0}] Produce acknowledged: {1}")]
    void ProduceAcknowledged(string producer, string messageType)
        => WriteEvent(ProduceAcknowledgedId, producer, messageType);

    [Event(ProduceNotAcknowledgedId, Level = EventLevel.Verbose, Message = "[{0}] Produce not acknowledged: {1} {2}")]
    void ProduceNotAcknowledged(string producer, string messageType, string error)
        => WriteEvent(ProduceNotAcknowledgedId, producer, messageType, error);
}