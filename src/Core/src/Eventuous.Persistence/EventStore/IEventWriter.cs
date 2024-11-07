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
}
