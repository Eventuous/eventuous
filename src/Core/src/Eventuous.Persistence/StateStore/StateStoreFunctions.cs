// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using Microsoft.Extensions.Caching.Memory;
using static Diagnostics.PersistenceEventSource;

public record FoldedEventStream<T> where T : State<T>, new() {
    public FoldedEventStream(StreamName streamName, ExpectedStreamVersion streamVersion, object[] events, T? initialState = null) {
        StreamName    = streamName;
        StreamVersion = streamVersion;
        Events        = events;
        State         = events.Aggregate(initialState ?? new T(), (state, o) => state.When(o));
    }

    public StreamName            StreamName    { get; }
    public ExpectedStreamVersion StreamVersion { get; }
    public object[]              Events        { get; }
    public T                     State         { get; init; }

    public void Deconstruct(out StreamName streamName, out ExpectedStreamVersion streamVersion, out object[] events) {
        streamName    = StreamName;
        streamVersion = StreamVersion;
        events        = Events;
    }
}

public static class StateStoreFunctions {
    public static Task<FoldedEventStream<T>> LoadState<T>(this IEventReader reader, StreamName streamName, IMemoryCache? memoryCache, CancellationToken cancellationToken)
        where T : State<T>, new()
        => reader.LoadEventsInternal<T>(streamName, true, memoryCache, cancellationToken);

    public static async Task<FoldedEventStream<T>> LoadState<T, TId>(this IEventReader reader, StreamNameMap streamNameMap, TId id, IMemoryCache? memoryCache, CancellationToken cancellationToken)
        where T : State<T>, new() where TId : Id {
        var foldedStream = await reader.LoadEventsInternal<T>(streamNameMap.GetStreamName(id), true, memoryCache, cancellationToken);
        return foldedStream with { State = foldedStream.State.WithId(id) };
    }

    public static Task<FoldedEventStream<T>> LoadStateOrNew<T>(this IEventReader reader, StreamName streamName, IMemoryCache? memoryCache, CancellationToken cancellationToken)
        where T : State<T>, new()
        => reader.LoadEventsInternal<T>(streamName, false, memoryCache, cancellationToken);

    public static async Task<FoldedEventStream<T>> LoadStateOrNew<T, TId>(this IEventReader reader, StreamNameMap streamNameMap, TId id, IMemoryCache? memoryCache, CancellationToken cancellationToken)
        where T : State<T>, new()
        where TId : Id {
        var foldedStream = await reader.LoadEventsInternal<T>(streamNameMap.GetStreamName(id), false, memoryCache, cancellationToken);
        return foldedStream with { State = foldedStream.State.WithId(id) };
    }

    static async Task<FoldedEventStream<T>> LoadEventsInternal<T>(
        this IEventReader reader,
        StreamName        streamName,
        bool              failIfNotFound,
        IMemoryCache?     memoryCache,
        CancellationToken cancellationToken
    ) where T : State<T>, new() {
        StreamReadPosition streamReadPosition;
        T? initialState;
        if (memoryCache?.TryGetValue(streamName, out Snapshot? snapshot) ?? false)
        {
            streamReadPosition = new StreamReadPosition(snapshot!.Version);
            initialState = ((Snapshot<T>)snapshot).State;
        }
        else
        {
            streamReadPosition = StreamReadPosition.Start;
            initialState = null;
        }

        try {
            var streamEvents = await reader.ReadStream(streamName, streamReadPosition, failIfNotFound, cancellationToken).NoContext();
            var events       = streamEvents.Select(x => x.Payload!).ToArray();
            var result       = new FoldedEventStream<T>(streamName, new ExpectedStreamVersion(streamEvents.Last().Position), events, initialState);
            if (events.Length > 0)
                memoryCache?.Set(streamName, new Snapshot<T>(result.State, result.StreamVersion.Value));
            return result;
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
