// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public class TieredEventReader(IEventReader hotReader, IEventReader archiveReader) : IEventReader {
    public async Task<StreamEvent[]> ReadEvents(StreamName streamName, StreamReadPosition start, int count, CancellationToken cancellationToken) {
        var hotEvents    = await LoadStreamEvents(hotReader, start, count).NoContext();
        var archivedEvents = hotEvents.Length == 0 || hotEvents[0].Position > start.Value
            ? await LoadStreamEvents(archiveReader, start, count - hotEvents.Length).NoContext()
            : Enumerable.Empty<StreamEvent>();

        return hotEvents.Concat(archivedEvents.Select(x => x with { FromArchive = true })).Distinct(Comparer).ToArray();

        async Task<StreamEvent[]> LoadStreamEvents(IEventReader reader, StreamReadPosition startPosition, int localCount) {
            try {
                return await reader.ReadEvents(streamName, startPosition, count, cancellationToken).NoContext();
            } catch (StreamNotFound) {
                return [];
            }
        }
    }

    public Task<StreamEvent[]> ReadEventsBackwards(StreamName stream, int count, CancellationToken cancellationToken) => throw new NotImplementedException();

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y) => x.Position == y.Position;

        public int GetHashCode(StreamEvent obj) => obj.Position.GetHashCode();
    }
}
