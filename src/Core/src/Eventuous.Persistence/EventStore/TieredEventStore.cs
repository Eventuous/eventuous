// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Event store that appends events to the hot store,
/// but reads from both hot store (recent events) and archive store (events missing from the hot store).
/// Truncation and deletion are only performed on hot store. Stream existence checks are performed on both stores.
/// It doesn't perform the archive itself, you need to use a connector to move events between hot and archive stores.
/// </summary>
/// <param name="hotStore"></param>
/// <param name="archiveReader"></param>
public class TieredEventStore(IEventStore hotStore, IEventReader archiveReader) : IEventStore {
    readonly TieredEventReader _tieredReader = new(Ensure.NotNull(hotStore), Ensure.NotNull(archiveReader));

    public Task<StreamEvent[]> ReadEvents(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
        => _tieredReader.ReadEvents(stream, start, count, cancellationToken);

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, StreamReadPosition start, int count, CancellationToken cancellationToken)
        => _tieredReader.ReadEventsBackwards(stream, start, count, cancellationToken);

    public Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        ) => hotStore.AppendEvents(stream, expectedVersion, events, cancellationToken);

    public async Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken = default) {
        var hotExists     = await hotStore.StreamExists(stream, cancellationToken);
        var archiveExists = archiveReader is IEventStore store && await store.StreamExists(stream, cancellationToken);

        return hotExists && archiveExists;
    }

    public Task TruncateStream(
            StreamName             stream,
            StreamTruncatePosition truncatePosition,
            ExpectedStreamVersion  expectedVersion,
            CancellationToken      cancellationToken = default
        )
        => hotStore.TruncateStream(stream, truncatePosition, expectedVersion, cancellationToken);

    public Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken = default)
        => hotStore.DeleteStream(stream, expectedVersion, cancellationToken);
}
