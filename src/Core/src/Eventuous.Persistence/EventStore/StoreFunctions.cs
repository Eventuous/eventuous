// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

using static Diagnostics.PersistenceEventSource;

public static class StoreFunctions {
    /// <summary>
    /// Stores a collection of events to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="streamName">Name of the stream where events will be appended to</param>
    /// <param name="originalVersion">Expected version of the stream in the event store</param>
    /// <param name="changes">Collection of events to store</param>
    /// <param name="amendEvent">Optional: function to add extra information to an event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Append events result</returns>
    /// <exception cref="Exception">Any exception that occurred in the event store</exception>
    /// <exception cref="OptimisticConcurrencyException">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static async Task<AppendEventsResult> Store(
            this IEventWriter           eventWriter,
            StreamName                  streamName,
            int                         originalVersion,
            IReadOnlyCollection<object> changes,
            AmendEvent?                 amendEvent,
            CancellationToken           cancellationToken
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
        } catch (Exception e) {
            throw e.InnerException?.Message.Contains("WrongExpectedVersion") == true
                ? new OptimisticConcurrencyException(streamName, e)
                : e;
        }

        StreamEvent ToStreamEvent(object evt, int position) {
            var streamEvent = new StreamEvent(Guid.NewGuid(), evt, new Metadata(), "", position);

            return amendEvent?.Invoke(streamEvent) ?? streamEvent;
        }
    }

    /// <summary>
    /// Store aggregate changes to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="streamName">Stream name for the aggregate</param>
    /// <param name="aggregate">Aggregate instance</param>
    /// <param name="amendEvent">Optional: function to add extra information to the event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <typeparam name="T">Aggregate type</typeparam>
    /// <returns>Append event result</returns>
    /// <exception cref="OptimisticConcurrencyException{T}">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static async Task<AppendEventsResult> Store<T>(
            this IEventWriter eventWriter,
            StreamName        streamName,
            T                 aggregate,
            AmendEvent?       amendEvent,
            CancellationToken cancellationToken
        ) where T : Aggregate {
        Ensure.NotNull(aggregate);

        try {
            return await eventWriter.Store(streamName, aggregate.OriginalVersion, aggregate.Changes, amendEvent, cancellationToken).NoContext();
        } catch (OptimisticConcurrencyException e) {
            Log.UnableToStoreAggregate<T>(streamName, e);

            throw new OptimisticConcurrencyException<T>(streamName, e.InnerException!);
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
                var events = await eventReader.ReadEvents(streamName, position, pageSize, cancellationToken).NoContext();
                streamEvents.AddRange(events);

                if (events.Length < pageSize) break;

                position = new StreamReadPosition(position.Value + events.Length);
            }
        } catch (StreamNotFound) when (!failIfNotFound) {
            return Array.Empty<StreamEvent>();
        }

        return streamEvents.ToArray();
    }
}
