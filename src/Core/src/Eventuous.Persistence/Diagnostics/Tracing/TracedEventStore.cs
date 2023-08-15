// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using static Constants;

public class TracedEventStore(IEventStore eventStore) : BaseTracer, IEventStore {
    public static IEventStore Trace(IEventStore eventStore)
        => new TracedEventStore(eventStore);

    IEventStore       Inner  { get; } = eventStore;
    TracedEventReader Reader { get; } = new(eventStore);
    TracedEventWriter Writer { get; } = new(eventStore);

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken)
        => Trace(stream, Operations.StreamExists, () => Inner.StreamExists(stream, cancellationToken));

    public Task<AppendEventsResult> AppendEvents(
            StreamName                       stream,
            ExpectedStreamVersion            expectedVersion,
            IReadOnlyCollection<StreamEvent> events,
            CancellationToken                cancellationToken
        )
        => Writer.AppendEvents(stream, expectedVersion, events, cancellationToken);

    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
        => Reader.ReadEvents(stream, start, count, cancellationToken);

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken)
        => Reader.ReadEventsBackwards(stream, count, cancellationToken);

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        )
        => Trace(stream, Operations.TruncateStream, () => Inner.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken));

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
        => Trace(stream, Operations.DeleteStream, () => Inner.DeleteStream(stream, expectedVersion, cancellationToken));
}
