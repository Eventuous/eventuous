// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable InvertIf

namespace Eventuous.Diagnostics.Tracing;

using static Constants;

public class TracedEventStore(IEventStore eventStore) : BaseTracer, IEventStore {
    public static IEventStore Trace(IEventStore eventStore) => new TracedEventStore(eventStore);

    readonly string _componentName = eventStore.GetType().Name;

    internal IEventStore Inner { get; } = eventStore;

    TracedEventReader Reader { get; } = new(eventStore);
    TracedEventWriter Writer { get; } = new(eventStore);

    public Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken)
        => Trace(stream, Operations.StreamExists, () => Inner.StreamExists(stream, cancellationToken));

    public Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        )
        => Writer.AppendEvents(stream, expectedVersion, events, cancellationToken);

    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Reader.ReadEvents(stream, start, count, failIfNotFound, cancellationToken);

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, bool failIfNotFound, CancellationToken cancellationToken)
        => Reader.ReadEventsBackwards(stream, start, count, failIfNotFound, cancellationToken);

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken
        )
        => Trace(stream, Operations.TruncateStream, () => Inner.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken));

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken)
        => Trace(stream, Operations.DeleteStream, () => Inner.DeleteStream(stream, expectedVersion, cancellationToken));

    // ReSharper disable once ConvertToAutoProperty
    protected override string ComponentName => _componentName;
}
