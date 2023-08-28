// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class StateStoreFunctions {
    public static Task<FoldedEventStream<T>> LoadState<T>(this IEventReader reader, StreamName streamName, CancellationToken cancellationToken)
        where T : State<T>, new()
        => reader.LoadEventsInternal<T>(streamName, true, cancellationToken);

    public static async Task<FoldedEventStream<T>> LoadState<T, TId>(this IEventReader reader, StreamNameMap streamNameMap, TId id, CancellationToken cancellationToken)
        where T : State<T>, new() where TId : Id {
        var foldedStream = await reader.LoadEventsInternal<T>(streamNameMap.GetStreamName(id), true, cancellationToken);
        return foldedStream with { State = foldedStream.State.WithId(id) };
    }

    public static Task<FoldedEventStream<T>> LoadStateOrNew<T>(this IEventReader reader, StreamName streamName, CancellationToken cancellationToken)
        where T : State<T>, new()
        => reader.LoadEventsInternal<T>(streamName, false, cancellationToken);

    public static async Task<FoldedEventStream<T>> LoadStateOrNew<T, TId>(this IEventReader reader, StreamNameMap streamNameMap, TId id, CancellationToken cancellationToken)
        where T : State<T>, new()
        where TId : Id {
        var foldedStream = await reader.LoadEventsInternal<T>(streamNameMap.GetStreamName(id), false, cancellationToken);
        return foldedStream with { State = foldedStream.State.WithId(id) };
    }

    static async Task<FoldedEventStream<T>> LoadEventsInternal<T>(
        this IEventReader reader,
        StreamName        streamName,
        bool              failIfNotFound,
        CancellationToken cancellationToken
    ) where T : State<T>, new() {
        try {
            var streamEvents = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
            var events       = streamEvents.Select(x => x.Payload!).ToArray();
            var expectedVersion = events.Length == 0
                ? ExpectedStreamVersion.NoStream
                : new ExpectedStreamVersion(streamEvents.Last().Position);
            return (new FoldedEventStream<T>(streamName, expectedVersion, events));
        }
        catch (StreamNotFound) when (!failIfNotFound) {
            return new FoldedEventStream<T>(streamName, ExpectedStreamVersion.NoStream, Array.Empty<object>());
        }
        catch (Exception e) {
            Log.UnableToLoadStream(streamName, e);
            throw;
        }
    }

    static TState WithId<TState, TId>(this TState state, TId id)
        where TState : State<TState>, new()
        where TId : Id {
        if (state is State<TState, TId> stateWithId) {
            stateWithId.Id = id;
        }

        return state;
    }
}
