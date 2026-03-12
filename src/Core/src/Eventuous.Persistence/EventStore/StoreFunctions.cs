// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class StoreFunctions {
    /// <param name="eventWriter">Event writer or event store</param>
    extension(IEventWriter eventWriter) {
        /// <summary>
        /// Stores a collection of events in the event store
        /// </summary>
        /// <param name="streamName">Name of the stream where events will be appended to</param>
        /// <param name="expectedStreamVersion">Expected version of the stream in the event store</param>
        /// <param name="changes">Collection of events to store</param>
        /// <param name="amendEvent">Optional: function to add extra information to an event before it gets stored</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Append events result</returns>
        /// <exception cref="Exception">Any exception that occurred in the event store</exception>
        /// <exception cref="OptimisticConcurrencyException">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
        [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
        [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
        public async Task<AppendEventsResult> Store(
                StreamName                  streamName,
                ExpectedStreamVersion       expectedStreamVersion,
                IReadOnlyCollection<object> changes,
                AmendEvent?                 amendEvent        = null,
                CancellationToken           cancellationToken = default
            ) {
            Ensure.NotNull(changes);

            if (changes.Count == 0) return AppendEventsResult.NoOp;

            try {
                var result = await eventWriter.AppendEvents(
                        streamName,
                        expectedStreamVersion,
                        changes.Select(ToStreamEvent).ToArray(),
                        cancellationToken
                    )
                    .NoContext();

                return result;
            } catch (Exception e) {
                throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                    ? new OptimisticConcurrencyException(streamName, e)
                    : e;
            }

            NewStreamEvent ToStreamEvent(object evt) {
                var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());

                return amendEvent?.Invoke(streamEvent) ?? streamEvent;
            }
        }

        [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
        [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
        public async Task<AppendEventsResult[]> Store(
                IReadOnlyCollection<(StreamName StreamName, ExpectedStreamVersion ExpectedVersion, IReadOnlyCollection<object> Changes)> streams,
                AmendEvent?                                                                                                              amendEvent        = null,
                CancellationToken                                                                                                        cancellationToken = default
            ) {
            if (streams.Count == 0) return [];

            var appends = streams.Select(s => {
                        Ensure.NotNull(s.Changes);

                        return new NewStreamAppend(
                            s.StreamName,
                            s.ExpectedVersion,
                            s.Changes.Select(evt => ToStreamEvent(evt, amendEvent)).ToArray()
                        );
                    }
                )
                .ToArray();

            try {
                return await eventWriter.AppendEvents(appends, cancellationToken).NoContext();
            } catch (Exception e) {
                throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                    ? new OptimisticConcurrencyException(
                        new StreamName(string.Join(", ", streams.Select(s => s.StreamName.ToString()))),
                        e
                    )
                    : e;
            }

            static NewStreamEvent ToStreamEvent(object evt, AmendEvent? amendEvent) {
                var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());

                return amendEvent?.Invoke(streamEvent) ?? streamEvent;
            }
        }
    }

    /// <summary>
    /// Read a fixed number of events from an existing stream to an array.
    /// Returns an empty array when the stream is not found and <paramref name="failIfNotFound"/> is false.
    /// </summary>
    /// <param name="eventReader">Event reader or event store</param>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="failIfNotFound">Throw an exception if the stream is not found</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An array with events retrieved from the stream</returns>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public static async Task<StreamEvent[]> ReadEvents(
            this IEventReader  eventReader,
            StreamName         stream,
            StreamReadPosition start,
            int                count,
            bool               failIfNotFound,
            CancellationToken  cancellationToken
        ) {
        try {
            var result = new List<StreamEvent>();

            await foreach (var evt in eventReader.ReadEvents(stream, start, count, cancellationToken).ConfigureAwait(false)) {
                result.Add(evt);
            }

            return result.ToArray();
        } catch (StreamNotFound) when (!failIfNotFound) {
            return [];
        }
    }

    /// <summary>
    /// Read a number of events from a given stream, backwards (from the stream end), to an array.
    /// Returns an empty array when the stream is not found and <paramref name="failIfNotFound"/> is false.
    /// </summary>
    /// <param name="eventReader">Event reader or event store</param>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="failIfNotFound">Throw an exception if the stream is not found</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An array with events retrieved from the stream</returns>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public static async Task<StreamEvent[]> ReadEventsBackwards(
            this IEventReader  eventReader,
            StreamName         stream,
            StreamReadPosition start,
            int                count,
            bool               failIfNotFound,
            CancellationToken  cancellationToken
        ) {
        try {
            var result = new List<StreamEvent>();

            await foreach (var evt in eventReader.ReadEventsBackwards(stream, start, count, cancellationToken).ConfigureAwait(false)) {
                result.Add(evt);
            }

            return result.ToArray();
        } catch (StreamNotFound) when (!failIfNotFound) {
            return [];
        }
    }

    /// <summary>
    /// Reads a stream from the event store to a collection of <seealso cref="StreamEvent"/>
    /// </summary>
    /// <param name="eventReader">Event reader or event store</param>
    /// <param name="streamName">Name of the stream to read from</param>
    /// <param name="start">Stream version to start reading from</param>
    /// <param name="failIfNotFound">Set to true if the function needs to throw when the stream isn't found. Default is false, and if there's no
    /// stream with the given name found in the store, the function will return an empty collection.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of events wrapped in <seealso cref="StreamEvent"/></returns>
    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public static async Task<StreamEvent[]> ReadStream(
            this IEventReader  eventReader,
            StreamName         streamName,
            StreamReadPosition start,
            bool               failIfNotFound    = true,
            CancellationToken  cancellationToken = default
        ) {
        const int pageSize = 500;

        var streamEvents = new List<StreamEvent>();

        var position = start;

        try {
            while (true) {
                var events = await eventReader.ReadEvents(streamName, position, pageSize, failIfNotFound, cancellationToken).NoContext();
                streamEvents.AddRange(events);

                if (events.Length < pageSize) break;

                position = new(position.Value + events.Length);
            }
        } catch (StreamNotFound) when (!failIfNotFound) {
            return [];
        }

        return streamEvents.ToArray();
    }

    [RequiresDynamicCode(AttrConstants.DynamicSerializationMessage)]
    [RequiresUnreferencedCode(AttrConstants.DynamicSerializationMessage)]
    public static async Task<StreamEvent[]> ReadStreamAfterSnapshot(
        this IEventReader eventReader,
        StreamName        streamName,
        HashSet<Type>     snapshotTypes,
        bool              failIfNotFound = true,
        CancellationToken cancellationToken = default
    ) {
        const int pageSize = 500;

        var streamEvents = new List<StreamEvent>();

        var position = StreamReadPosition.End;

        try {
            while (true) {
                var events = await eventReader.ReadEventsBackwards(streamName, position, pageSize, failIfNotFound, cancellationToken).NoContext();

                var snapshotIndex = (int?) null;

                for (var i = 0; i < events.Length; i++) {
                    var payload = events[i].Payload;
                    if (payload is not null && snapshotTypes.Contains(payload.GetType())) {
                        snapshotIndex = i;
                        break;
                    }
                }

                if (snapshotIndex.HasValue) {
                    streamEvents.AddRange(events[..(snapshotIndex.Value + 1)]);
                    break;
                } else {
                    streamEvents.AddRange(events);
                }

                if (events.Length < pageSize) break;

                position = new(position.Value - events.Length);
            }
        } catch (StreamNotFound) when (!failIfNotFound) {
            return [];
        }

        streamEvents.Reverse();
        return [.. streamEvents];
    }
}
