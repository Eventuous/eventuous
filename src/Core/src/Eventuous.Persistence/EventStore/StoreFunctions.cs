// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

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
                        [.. changes.Select(ToStreamEvent)],
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
                            [.. s.Changes.Select(evt => ToStreamEvent(evt, amendEvent))]
                        );
                    }
                )
                .ToArray();

            try {
                return await eventWriter.AppendEvents(appends, cancellationToken).NoContext();
            } catch (Exception e) {
                throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                    ? new OptimisticConcurrencyException(new(string.Join(", ", streams.Select(s => s.StreamName.ToString()))), e)
                    : e;
            }

            static NewStreamEvent ToStreamEvent(object evt, AmendEvent? amendEvent) {
                var streamEvent = new NewStreamEvent(Guid.NewGuid(), evt, new());

                return amendEvent?.Invoke(streamEvent) ?? streamEvent;
            }
        }
    }

    /// <param name="eventReader">Event reader or event store</param>
    extension(IEventReader eventReader) {
        /// <summary>
        /// Read a fixed number of events from an existing stream to an array.
        /// Returns an empty array when the stream is not found and <paramref name="failIfNotFound"/> is false.
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="start">Where to start reading events</param>
        /// <param name="count">How many events to read</param>
        /// <param name="failIfNotFound">Throw an exception if the stream is not found</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An array with events retrieved from the stream</returns>
        public async Task<StreamEvent[]> ReadEvents(
                StreamName         stream,
                StreamReadPosition start,
                int                count,
                bool               failIfNotFound,
                CancellationToken  cancellationToken
            ) {
            try {
                var result = new List<StreamEvent>();

                await foreach (var evt in eventReader.ReadEvents(stream, start, count, cancellationToken).NoContext(cancellationToken)) {
                    result.Add(evt);
                }

                return [.. result];
            } catch (StreamNotFound) when (!failIfNotFound) {
                return [];
            }
        }

        /// <summary>
        /// Read a number of events from a given stream, backwards (from the stream end), to an array.
        /// Returns an empty array when the stream is not found and <paramref name="failIfNotFound"/> is false.
        /// </summary>
        /// <param name="stream">Stream name</param>
        /// <param name="start">Where to start reading events</param>
        /// <param name="count">How many events to read</param>
        /// <param name="failIfNotFound">Throw an exception if the stream is not found</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An array with events retrieved from the stream</returns>
        public async Task<StreamEvent[]> ReadEventsBackwards(
                StreamName         stream,
                StreamReadPosition start,
                int                count,
                bool               failIfNotFound,
                CancellationToken  cancellationToken
            ) {
            try {
                var result = new List<StreamEvent>();

                await foreach (var evt in eventReader.ReadEventsBackwards(stream, start, count, cancellationToken).NoContext(cancellationToken)) {
                    result.Add(evt);
                }

                return [.. result];
            } catch (StreamNotFound) when (!failIfNotFound) {
                return [];
            }
        }

        /// <summary>
        /// Reads a stream from the given position to the end, as an async enumerable.
        /// Events are read in pages of <paramref name="pageSize"/> and yielded as they arrive, so the whole stream
        /// is never buffered in memory. Use this instead of calling <see cref="IEventReader.ReadEvents"/>
        /// with <see cref="int.MaxValue"/> as the count.
        /// </summary>
        /// <param name="streamName">Name of the stream to read from</param>
        /// <param name="start">Stream position to start reading from</param>
        /// <param name="pageSize">Number of events to read per page. It bounds the memory a buffering
        /// implementation of <see cref="IEventReader"/> uses: such implementations hold at most a small
        /// multiple of a page in memory at a time (e.g. a tiered reader combining two stores).</param>
        /// <param name="failIfNotFound">Set to false to complete without yielding anything when the stream isn't found,
        /// instead of throwing <see cref="StreamNotFound"/>. Default is true.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>An async enumerable of events retrieved from the stream</returns>
        public IAsyncEnumerable<StreamEvent> ReadStreamToEnd(
                StreamName        streamName,
                StreamReadPosition start,
                int               pageSize          = 500,
                bool              failIfNotFound    = true,
                CancellationToken cancellationToken = default
            ) {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageSize);

            return ReadToEnd(eventReader, streamName, start, pageSize, failIfNotFound, cancellationToken);
        }

        /// <summary>
        /// Reads a stream from the event store to a collection of <seealso cref="StreamEvent"/>
        /// </summary>
        /// <param name="streamName">Name of the stream to read from</param>
        /// <param name="start">Stream version to start reading from</param>
        /// <param name="failIfNotFound">Set to true if the function needs to throw when the stream isn't found. Default is false, and if there's no
        /// stream with the given name found in the store, the function will return an empty collection.</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of events wrapped in <seealso cref="StreamEvent"/></returns>
        public async Task<StreamEvent[]> ReadStream(
                StreamName         streamName,
                StreamReadPosition start,
                bool               failIfNotFound    = true,
                CancellationToken  cancellationToken = default
            ) {
            var streamEvents = new List<StreamEvent>();

            await foreach (var evt in eventReader.ReadStreamToEnd(streamName, start, failIfNotFound: failIfNotFound, cancellationToken: cancellationToken).NoContext(cancellationToken)) {
                streamEvents.Add(evt);
            }

            return [.. streamEvents];
        }
    }

    // Relies on readers yielding exactly `count` events unless the stream end is reached:
    // a page shorter than pageSize means there is nothing left to read
    static async IAsyncEnumerable<StreamEvent> ReadToEnd(
            IEventReader                               eventReader,
            StreamName                                 streamName,
            StreamReadPosition                         start,
            int                                        pageSize,
            bool                                       failIfNotFound,
            [EnumeratorCancellation] CancellationToken cancellationToken
        ) {
        var position = start;

        while (true) {
            var  yielded      = 0;
            long lastRevision = 0;

            await using var enumerator = eventReader.ReadEvents(streamName, position, pageSize, cancellationToken).GetAsyncEnumerator(cancellationToken);

            while (true) {
                bool moved;

                try {
                    moved = await enumerator.MoveNextAsync().NoContext();
                } catch (StreamNotFound) when (!failIfNotFound) {
                    yield break;
                }

                if (!moved) break;

                var evt = enumerator.Current;
                yielded++;
                lastRevision = evt.Revision;

                yield return evt;
            }

            if (yielded < pageSize) yield break;

            // The maximum revision is the end of the representable position space
            if (lastRevision == long.MaxValue) yield break;

            position = new(lastRevision + 1);
        }
    }
}
