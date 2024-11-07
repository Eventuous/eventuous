// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public static class StoreFunctions {
    /// <summary>
    /// Stores a collection of events to the event store
    /// </summary>
    /// <param name="eventWriter">Event writer or event store</param>
    /// <param name="streamName">Name of the stream where events will be appended to</param>
    /// <param name="expectedStreamVersion">Expected version of the stream in the event store</param>
    /// <param name="changes">Collection of events to store</param>
    /// <param name="amendEvent">Optional: function to add extra information to an event before it gets stored</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Append events result</returns>
    /// <exception cref="Exception">Any exception that occurred in the event store</exception>
    /// <exception cref="OptimisticConcurrencyException">Gets thrown if the expected stream version mismatches with the given original stream version</exception>
    public static async Task<AppendEventsResult> Store(
            this IEventWriter           eventWriter,
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
            bool               failIfNotFound    = true,
            CancellationToken  cancellationToken = default
        ) {
        const int pageSize = 500;

        var streamEvents = new List<StreamEvent>();

        var position = start;

        try {
            while (true) {
                var events = await eventReader.ReadEvents(streamName, position, pageSize, cancellationToken).NoContext();
                streamEvents.AddRange(events);

                if (events.Length < pageSize) break;

                position = new(position.Value + events.Length);
            }
        } catch (StreamNotFound) when (!failIfNotFound) {
            return [];
        }

        return streamEvents.ToArray();
    }
}
