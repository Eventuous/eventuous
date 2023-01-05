// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics.Tracing;

// ReSharper disable MemberCanBePrivate.Global

namespace Eventuous.Diagnostics;

[EventSource(Name = $"{DiagnosticName.BaseName}.persistence")]
public class PersistenceEventSource : EventSource {
    public static readonly PersistenceEventSource Log = new();

    const int UnableToStoreAggregateId  = 5;
    const int UnableToReadAggregateId   = 6;
    const int UnableToAppendEventsId    = 7;

    [NonEvent]
    public void UnableToLoadAggregate<T>(StreamName streamName, Exception exception)
        where T : Aggregate {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            UnableToLoadAggregate(typeof(T).Name, streamName, exception.ToString());
    }

    [NonEvent]
    public void UnableToStoreAggregate<T>(StreamName streamName, Exception exception)
        where T : Aggregate {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            UnableToStoreAggregate(typeof(T).Name, streamName, exception.ToString());
    }

    [NonEvent]
    public void UnableToAppendEvents(string stream, Exception exception) {
        if (IsEnabled(EventLevel.Warning, EventKeywords.All))
            UnableToAppendEvents(stream, exception.ToString());
    }

    [Event(UnableToAppendEventsId, Message = "Unable to append events to {0}: {1}", Level = EventLevel.Error)]
    void UnableToAppendEvents(string stream, string exception)
        => WriteEvent(UnableToAppendEventsId, stream, exception);

    [Event(
        UnableToStoreAggregateId,
        Message = "Unable to store aggregate {0} to stream {2}: {3}",
        Level = EventLevel.Warning
    )]
    void UnableToStoreAggregate(string type, string stream, string exception)
        => WriteEvent(UnableToStoreAggregateId, type, stream, exception);

    [Event(
        UnableToReadAggregateId,
        Message = "Unable to read aggregate {0} with from stream {1}: {2}",
        Level = EventLevel.Warning
    )]
    void UnableToLoadAggregate(string type, string stream, string exception)
        => WriteEvent(UnableToReadAggregateId, type, stream, exception);
}