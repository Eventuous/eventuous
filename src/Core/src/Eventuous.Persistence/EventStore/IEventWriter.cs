// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

public interface IEventWriter {
    /// <summary>
    /// Append one or more events to a stream
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="expectedVersion">Expected stream version (can be Any)</param>
    /// <param name="events">Collection of events to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Append result, which contains the global position of the last written event,
    /// as well as the next stream version</returns>
    Task<AppendEventsResult> AppendEvents(
            StreamName                          stream,
            ExpectedStreamVersion               expectedVersion,
            IReadOnlyCollection<NewStreamEvent> events,
            CancellationToken                   cancellationToken
        );

    /// <summary>
    /// Append events to multiple streams. Default implementation calls single-stream AppendEvents
    /// sequentially with fail-fast semantics (no cross-stream atomicity guarantee).
    /// Stores that support atomic multi-stream writes should override this method.
    /// </summary>
    /// <param name="appends">Collection of stream appends to perform</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of append results, one per stream in the same order as input</returns>
    async Task<AppendEventsResult[]> AppendEvents(IReadOnlyCollection<NewStreamAppend> appends, CancellationToken cancellationToken) {
        var results = new AppendEventsResult[appends.Count];
        var i       = 0;

        foreach (var append in appends) {
            Ensure.NotNull(append.Events);
            if (append.Events.Count == 0) throw new InvalidOperationException($"Append to stream {append.StreamName} has no events");

            results[i++] = await AppendEvents(append.StreamName, append.ExpectedVersion, append.Events, cancellationToken).NoContext();
        }

        return results;
    }
}
