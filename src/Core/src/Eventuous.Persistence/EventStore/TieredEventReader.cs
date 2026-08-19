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
        var (hotEvents, hotNotFound) = await LoadStreamEvents(hotReader, streamName, start, count, cancellationToken).NoContext();

        IEnumerable<StreamEvent> archivedEvents;
        var                      archiveNotFound = false;

        switch (hotEvents.Length) {
            case > 0 when hotEvents[0].Revision > start.Value: {
                // Fill the gap before the first hot event from the archive, bounded by the requested count
                var gapCount = (int)Math.Min(count, hotEvents[0].Revision - start.Value);

                (var events, archiveNotFound) = await LoadStreamEvents(archiveReader, streamName, start, gapCount, cancellationToken).NoContext();
                archivedEvents                = events.Select(x => x with { FromArchive = true });

                break;
            }
            case 0:
                (var archived, archiveNotFound) = await LoadStreamEvents(archiveReader, streamName, start, count, cancellationToken).NoContext();
                archivedEvents                  = archived.Select(x => x with { FromArchive = true }); break;
            default:
                archivedEvents = []; break;
        }

        var combined = archivedEvents.Concat(hotEvents).Distinct(Comparer).Take(count);
        var any      = false;

        foreach (var evt in combined) {
            any = true;

            yield return evt;
        }

        // No events with both tiers reporting a missing stream means the stream doesn't exist;
        // otherwise an empty result can mean the read window is past the stream end
        if (!any && hotNotFound && archiveNotFound) throw new StreamNotFound(streamName);
    }

    public async IAsyncEnumerable<StreamEvent> ReadEventsBackwards(StreamName streamName, StreamReadPosition start, int count, [EnumeratorCancellation] CancellationToken cancellationToken) {
        var (hotEvents, hotNotFound) = await LoadStreamEvents(hotReader, streamName, start, count, cancellationToken, backwards: true).NoContext();

        IEnumerable<StreamEvent> archivedEvents;
        var                      archiveNotFound = false;

        switch (hotEvents.Length) {
            // When the hot store read reached revision 0, no events can precede it
            case > 0 when hotEvents.Length < count && hotEvents[^1].Revision > 0: {
                // Hot store returned fewer events than requested, fill the gap from archive
                var lastHotRevision = hotEvents[^1].Revision;

                (var events, archiveNotFound) = await LoadStreamEvents(archiveReader, streamName, new(lastHotRevision - 1), count - hotEvents.Length, cancellationToken, backwards: true).NoContext();
                archivedEvents                = events.Select(x => x with { FromArchive = true });

                break;
            }
            case 0:
                // Hot store has no events, try archive for the full range
                (var archived, archiveNotFound) = await LoadStreamEvents(archiveReader, streamName, start, count, cancellationToken, backwards: true).NoContext();
                archivedEvents                  = archived.Select(x => x with { FromArchive = true }); break;
            default:
                archivedEvents = []; break;
        }

        var combined = hotEvents.Concat(archivedEvents).Distinct(Comparer);
        var any      = false;

        foreach (var evt in combined) {
            any = true;

            yield return evt;
        }

        // No events with both tiers reporting a missing stream means the stream doesn't exist;
        // otherwise an empty result can mean the read window is past the stream end
        if (!any && hotNotFound && archiveNotFound) throw new StreamNotFound(streamName);
    }

    static async Task<(StreamEvent[] Events, bool NotFound)> LoadStreamEvents(
            IEventReader       reader,
            StreamName         streamName,
            StreamReadPosition startPosition,
            int                localCount,
            CancellationToken  cancellationToken,
            bool               backwards = false
        ) {
        try {
            var events = backwards
                ? await reader.ReadEventsBackwards(streamName, startPosition, localCount, true, cancellationToken).NoContext()
                : await reader.ReadEvents(streamName, startPosition, localCount, true, cancellationToken).NoContext();

            return (events, false);
        } catch (StreamNotFound) {
            return ([], true);
        }
    }

    static readonly StreamEventPositionComparer Comparer = new();

    class StreamEventPositionComparer : IEqualityComparer<StreamEvent> {
        public bool Equals(StreamEvent x, StreamEvent y) => x.Revision == y.Revision;

        public int GetHashCode(StreamEvent obj) => obj.Revision.GetHashCode();
    }
}
