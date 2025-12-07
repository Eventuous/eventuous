// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class StateStoreFunctions {
    /// <param name="reader">Event reader or event store</param>
    extension(IEventReader reader) {
        /// <summary>
        /// Reads the event stream and folds it into a state object. This function will fail if the stream does not exist.
        /// </summary>
        /// <param name="streamName">Name of the stream to read from</param>
        /// <param name="failIfNotFound">When set to false and there's no stream, the function will return an empty instance.</param>
        /// <param name="snapshotStore">Optional snapshot store for SeparateStore strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <typeparam name="TState">State object type</typeparam>
        /// <returns>Instance of <seealso cref="FoldedEventStream{T}"/> containing events and folded state</returns>
        /// <exception cref="StreamNotFound">Thrown if there's no stream and failIfNotFound is true</exception>
        [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
        [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
        public async Task<FoldedEventStream<TState>> LoadState<TState>(
                StreamName        streamName,
                bool              failIfNotFound    = true,
                ISnapshotStore?   snapshotStore     = null,
                CancellationToken cancellationToken = default
            ) where TState : State<TState>, new() {
            try {
                StreamEvent[] streamEvents;
                var snapshotTypes = SnapshotTypeMap.GetSnapshotTypes<TState>();
                var storageStrategy = SnapshotTypeMap.GetStorageStrategy<TState>();

                if (snapshotTypes.Count != 0) {
                    switch (storageStrategy) {
                        case SnapshotStorageStrategy.SameStream:
                            streamEvents = await reader.ReadStreamAfterSnapshot(streamName, snapshotTypes, failIfNotFound, cancellationToken);
                            break;

                        case SnapshotStorageStrategy.SeparateStream: {
                            var snapshotStreamName = StreamName.ForSnapshot(streamName);
                            var snapshotEvents = await reader.ReadEventsBackwards(snapshotStreamName, StreamReadPosition.End, 1, false, cancellationToken).NoContext();

                            StreamEvent? snapshotEvent = null;

                            if (snapshotEvents.Length > 0) {
                                var candidate = snapshotEvents[0];
                                if (candidate.Payload != null && snapshotTypes.Contains(candidate.Payload.GetType())) {
                                    snapshotEvent = candidate;
                                }
                            }

                            if (snapshotEvent.HasValue) {
                                var snapshotRevision = snapshotEvent.Value.Revision;
                                var eventsAfterSnapshot = await reader.ReadStream(streamName, new(snapshotRevision + 1), failIfNotFound, cancellationToken).NoContext();
                                streamEvents = [snapshotEvent.Value, ..eventsAfterSnapshot];
                            } else {
                                streamEvents = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
                            }
                            break;
                        }

                        case SnapshotStorageStrategy.SeparateStore: {
                            if (snapshotStore == null) {
                                throw new InvalidOperationException($"Snapshot store is required for {nameof(SnapshotStorageStrategy.SeparateStore)} strategy");
                            }

                            var snapshot = await snapshotStore.Read(streamName, cancellationToken).NoContext();

                            if (snapshot != null) {
                                var snapshotEvent = new StreamEvent(
                                    Guid.Empty,
                                    snapshot.Payload,
                                    [],
                                    string.Empty,
                                    snapshot.Revision
                                );
                                var eventsAfterSnapshot = await reader.ReadStream(streamName, new(snapshot.Revision + 1), failIfNotFound, cancellationToken).NoContext();
                                streamEvents = [snapshotEvent, ..eventsAfterSnapshot];
                            } else {
                                streamEvents = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
                            }
                            break;
                        }

                        default:
                            streamEvents = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
                            break;
                    }
                } else {
                    streamEvents = await reader.ReadStream(streamName, StreamReadPosition.Start, failIfNotFound, cancellationToken).NoContext();
                }

                var events = streamEvents.Select(x => x.Payload!).ToArray();
                var expectedVersion = events.Length == 0 ? ExpectedStreamVersion.NoStream : new(streamEvents.Last().Revision);

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
        /// <param name="id">State identity value</param>
        /// <param name="failIfNotFound">When set to false and there's no stream, the function will return an empty instance.</param>
        /// <param name="snapshotStore">Optional snapshot store for SeparateStore strategy</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="streamNameMap">Mapper between identity and stream name</param>
        /// <typeparam name="TState">State object type</typeparam>
        /// <typeparam name="TId">State identity type</typeparam>
        /// <returns>Instance of <seealso cref="FoldedEventStream{T}"/> containing events and folded state</returns>
        [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
        [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
        public async Task<FoldedEventStream<TState>> LoadState<TState, TId>(
                StreamNameMap     streamNameMap,
                TId               id,
                bool              failIfNotFound    = true,
                ISnapshotStore?   snapshotStore    = null,
                CancellationToken cancellationToken = default
            )
            where TState : State<TState>, new() where TId : Id {
            var foldedStream = await reader.LoadState<TState>(streamNameMap.GetStreamName(id), failIfNotFound, snapshotStore, cancellationToken).NoContext();

            return foldedStream with { State = foldedStream.State.WithId(id) };
        }
    }

    static TState WithId<TState, TId>(this TState state, TId id) where TState : State<TState>, new() where TId : Id {
        if (state is State<TState, TId> stateWithId) {
            stateWithId.Id = id;
        }

        return state;
    }
}
