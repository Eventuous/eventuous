// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class StoreFunctions {
    public static async Task<AppendEventsResult> Store(
        this IEventWriter              eventWriter,
        StreamName                     streamName,
        int                            originalVersion,
        IReadOnlyCollection<object>    changes,
        Func<StreamEvent, StreamEvent> amendEvent,
        CancellationToken              cancellationToken
    ) {
        Ensure.NotNull(changes);

        if (changes.Count == 0) return AppendEventsResult.NoOp;

        var expectedVersion = new ExpectedStreamVersion(originalVersion);

        try {
            var result = await eventWriter.AppendEvents(
                    streamName,
                    expectedVersion,
                    changes.Select((o, i) => ToStreamEvent(o, i + originalVersion)).ToArray(),
                    cancellationToken
                )
                .NoContext();

            return result;
        }
        catch (Exception e) {
            throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                ? new OptimisticConcurrencyException(streamName, e)
                : e;
        }

        StreamEvent ToStreamEvent(object evt, int position) {
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", position);
            return amendEvent(streamEvent);
        }
    }

    public static async Task<AppendEventsResult> Store<T>(
        this IEventWriter              eventWriter,
        StreamName                     streamName,
        T                              aggregate,
        Func<StreamEvent, StreamEvent> amendEvent,
        CancellationToken              cancellationToken
    ) where T : Aggregate {
        Ensure.NotNull(aggregate);

        try {
            return await eventWriter.Store(streamName, aggregate.OriginalVersion, aggregate.Changes, amendEvent, cancellationToken).NoContext();
        }
        catch (OptimisticConcurrencyException e) {
            Log.UnableToStoreAggregate<T>(streamName, e);
            throw new OptimisticConcurrencyException<T>(streamName, e.InnerException!);
        }
    }

    public static async Task<StreamEvent[]> ReadStream(
        this IEventReader  eventReader,
        StreamName         streamName,
        StreamReadPosition start,
        bool               failIfNotFound,
        CancellationToken  cancellationToken
    ) {
        const int pageSize = 500;

        var streamEvents = new List<StreamEvent>();

        var position = start;

        try {
            while (true) {
                var events = await eventReader.ReadEvents(
                        streamName,
                        position,
                        pageSize,
                        cancellationToken
                    )
                    .NoContext();

                streamEvents.AddRange(events);

                if (events.Length < pageSize) break;

                position = new StreamReadPosition(position.Value + events.Length);
            }
        }
        catch (StreamNotFound) when (!failIfNotFound) {
            return Array.Empty<StreamEvent>();
        }

        return streamEvents.ToArray();
    }
}
