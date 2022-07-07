// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using static Eventuous.Diagnostics.EventuousEventSource;

namespace Eventuous;

public static class StoreFunctions {
    public static async Task<AppendEventsResult> Store<T>(
        this IEventWriter              eventWriter,
        StreamName                     streamName,
        T                              aggregate,
        Func<StreamEvent, StreamEvent> amendEvent,
        CancellationToken              cancellationToken
    ) where T : Aggregate {
        Ensure.NotNull(aggregate);

        if (aggregate.Changes.Count == 0) return AppendEventsResult.NoOp;

        var originalVersion = aggregate.OriginalVersion;
        var expectedVersion = new ExpectedStreamVersion(originalVersion);

        try {
            var result = await eventWriter.AppendEvents(
                    streamName,
                    expectedVersion,
                    aggregate.Changes.Select((o, i) => ToStreamEvent(o, i + originalVersion)).ToArray(),
                    cancellationToken
                )
                .NoContext();

            return result;
        }
        catch (Exception e) {
            Log.UnableToStoreAggregate<T>(streamName, e);

            throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                ? new OptimisticConcurrencyException<T>(streamName, e) : e;
        }

        StreamEvent ToStreamEvent(object evt, int position) {
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", position);
            return amendEvent(streamEvent);
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
