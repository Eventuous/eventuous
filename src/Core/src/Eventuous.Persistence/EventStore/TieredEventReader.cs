// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Event reader that reads from both hot store (recent events) and archive store (events missing from the hot store).
/// It doesn't perform the archive itself, you need to use a connector to move events between hot and archive stores.
/// </summary>
/// <param name="hotReader">Event reader pointing to hot store</param>
/// <param name="archiveReader">Event reader pointing to archive store</param>
public class TieredEventReader(IEventReader hotReader, IEventReader archiveReader) : IEventReader {
    public async Task<StreamEvent[]> ReadEvents(StreamName streamName, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        var hotEvents = await LoadStreamEvents(hotReader, start, count).NoContext();

        var archivedEvents = hotEvents.Length == 0 || hotEvents[0].Position > start.Value
            ? await LoadStreamEvents(archiveReader, start, (int)hotEvents[0].Position).NoContext()
            : Enumerable.Empty<StreamEvent>();

        return archivedEvents.Select(x => x with { FromArchive = true }).Concat(hotEvents).Distinct(Comparer).ToArray();

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition startPosition, int localCount) {
            try {
                return await reader.ReadEvents(streamName, startPosition, localCount, cancellationToken).NoContext();
            } catch (StreamNotFound) {
                return [];
            }
        }
    }

    public async Task<StreamEvent[]> ReadEventsBackwards(StreamName streamName, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        var hotEvents = await LoadStreamEvents(hotReader, start, count).NoContext();

        var archivedEvents = hotEvents.Length == 0 || hotEvents[0].Position > start.Value - count
            ? await LoadStreamEvents(archiveReader, new(hotEvents[0].Position - 1), count - hotEvents.Length).NoContext()
            : Enumerable.Empty<StreamEvent>();

        return hotEvents.Concat(archivedEvents.Select(x => x with { FromArchive = true })).Distinct(Comparer).ToArray();

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition startPosition, int localCount) {
            try {
                return await reader.ReadEventsBackwards(streamName, startPosition, localCount, cancellationToken).NoContext();
            } catch (StreamNotFound) {
                return [];
            }
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y) => x.Position == y.Position;

        public int GetHashCode(StreamEvent obj) => obj.Position.GetHashCode();
    }
}
