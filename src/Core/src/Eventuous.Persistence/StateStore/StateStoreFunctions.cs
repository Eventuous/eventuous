// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class StateStoreFunctions {
    /// <summary>
    /// Reads the event stream and folds it into a state object. This function will fail if the stream does not exist.
    /// </summary>
    /// <param name="reader">Event reader or event store</param>
    /// <param name="streamName">Name of the stream to read from</param>
    /// <param name="failIfNotFound">When set to false and there's no stream, the function will return an empty instance.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="TState">State object type</typeparam>
    /// <returns>Instance of <seealso cref="FoldedEventStream{T}"/> containing events and folded state</returns>
    /// <exception cref="StreamNotFound">Thrown if there's no stream and failIfNotFound is true</exception>
    public static async Task<FoldedEventStream<TState>> LoadState<TState>(
            this IEventReader reader,
            StreamName        streamName,
            bool              failIfNotFound    = true,
            CancellationToken cancellationToken = default
        ) where TState : State<TState>, new() {
        try {
            var streamEvents    = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
            var events          = streamEvents.Select(x => x.Payload!).ToArray();
            var expectedVersion = events.Length == 0 ? ExpectedStreamVersion.NoStream : new(streamEvents.Last().Position);

            return (new(streamName, expectedVersion, events));
        } catch (StreamNotFound) when (!failIfNotFound) {
            return new(streamName, ExpectedStreamVersion.NoStream, []);
        } catch (Exception e) {
            Log.UnableToLoadStream(streamName, e);

            throw;
        }
    }

    /// <summary>
    /// Reads the event stream and folds it into a state object. This function will fail if the stream does not exist.
    /// </summary>
    /// <param name="reader">Event reader or event store</param>
    /// <param name="id">State identity value</param>
    /// <param name="failIfNotFound">When set to false and there's no stream, the function will return an empty instance.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="streamNameMap">Mapper between identity and stream name</param>
    /// <typeparam name="TState">State object type</typeparam>
    /// <typeparam name="TId">State identity type</typeparam>
    /// <returns>Instance of <seealso cref="FoldedEventStream{T}"/> containing events and folded state</returns>
    public static async Task<FoldedEventStream<TState>> LoadState<TState, TId>(
            this IEventReader reader,
            StreamNameMap     streamNameMap,
            TId               id,
            bool              failIfNotFound    = true,
            CancellationToken cancellationToken = default
        )
        where TState : State<TState>, new() where TId : Id {
        var foldedStream = await reader.LoadState<TState>(streamNameMap.GetStreamName(id), failIfNotFound, cancellationToken).NoContext();

        return foldedStream with { State = foldedStream.State.WithId(id) };
    }

    static TState WithId<TState, TId>(this TState state, TId id) where TState : State<TState>, new() where TId : Id {
        if (state is State<TState, TId> stateWithId) {
            stateWithId.Id = id;
        }

        return state;
    }
}
