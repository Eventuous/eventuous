// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous;

/// <summary>
/// Event reader that reads from both hot store (recent events) and archive store (events missing from the hot store).
/// It doesn't perform the archive itself, you need to use a connector to move events between hot and archive stores.
/// </summary>
/// <param name="hotReader">Event reader pointing to hot store</param>
/// <param name="archiveReader">Event reader pointing to archive store</param>
public class TieredEventReader(IEventReader hotReader, IEventReader archiveReader) : IEventReader {
    public async IAsyncEnumerable<StreamEvent> ReadEvents(StreamName streamName, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
        var hotEvents = await LoadStreamEvents(hotReader, streamName, start, count, cancellationToken).NoContext();

        IEnumerable<StreamEvent> archivedEvents;

        if (hotEvents.Length > 0 && hotEvents[0].Revision > start.Value) {
            // Hot store has events but they start after the requested position, fill the gap from archive
            archivedEvents = (await LoadStreamEvents(archiveReader, streamName, start, (int)hotEvents[0].Revision, cancellationToken).NoContext())
                .Select(x => x with { FromArchive = true });
        } else if (hotEvents.Length == 0) {
            // Hot store has no events, try archive for the full range
            archivedEvents = (await LoadStreamEvents(archiveReader, streamName, start, count, cancellationToken).NoContext())
                .Select(x => x with { FromArchive = true });
        } else {
            archivedEvents = [];
        }

        var combined = archivedEvents.Concat(hotEvents).Distinct(Comparer);
        var any      = false;

        foreach (var evt in combined) {
            any = true;
            yield return evt;
        }

        if (!any) throw new StreamNotFound(streamName);
    }

    public async IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName streamName, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
        var hotEvents = await LoadStreamEvents(hotReader, streamName, start, count, cancellationToken, backwards: true).NoContext();

        IEnumerable<StreamEvent> archivedEvents;

        if (hotEvents.Length > 0 && hotEvents.Length < count) {
            // Hot store returned fewer events than requested, fill the gap from archive
            var lastHotRevision = hotEvents[^1].Revision;
            archivedEvents = (await LoadStreamEvents(archiveReader, streamName, new(lastHotRevision - 1), count - hotEvents.Length, cancellationToken, backwards: true).NoContext())
                .Select(x => x with { FromArchive = true });
        } else if (hotEvents.Length == 0) {
            // Hot store has no events, try archive for the full range
            archivedEvents = (await LoadStreamEvents(archiveReader, streamName, start, count, cancellationToken, backwards: true).NoContext())
                .Select(x => x with { FromArchive = true });
        } else {
            archivedEvents = [];
        }

        var combined = hotEvents.Concat(archivedEvents).Distinct(Comparer);
        var any      = false;

        foreach (var evt in combined) {
            any = true;
            yield return evt;
        }

        if (!any) throw new StreamNotFound(streamName);
    }

    static async Task<StreamEvent[]> LoadStreamEvents(
            IEventReader      reader,
            StreamName        streamName,
            StreamReadPosition startPosition,
            int               localCount,
            CancellationToken cancellationToken,
            bool              backwards = false
        ) {
        try {
            return backwards
                ? await reader.ReadEventsBackwards(streamName, startPosition, localCount, true, cancellationToken).NoContext()
                : await reader.ReadEvents(streamName, startPosition, localCount, true, cancellationToken).NoContext();
        } catch (StreamNotFound) {
            return [];
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y) => x.Revision == y.Revision;

        public int GetHashCode(StreamEvent obj) => obj.Revision.GetHashCode();
    }
}
