// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Event Store is a place where events are stored. It is used by <see cref="AggregateStore"/> and
/// <seealso cref="StateStore"/>
/// </summary>
public interface IEventStore : IEventReader, IEventWriter {
    /// <summary>
    /// Checks if a given stream exists in the store
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="cancellationToken"></param>
    /// <returns>True of stream exists</returns>
    Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken = default);

    /// <summary>
    /// Truncate a stream at a given position
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="truncatePosition">Where to truncate the stream</param>
    /// <param name="expectedVersion">Expected stream version (could be Any)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task TruncateStream(StreamName stream, StreamTruncatePosition truncatePosition, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a stream
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="expectedVersion">Expected stream version (could be Any)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task DeleteStream(StreamName stream, ExpectedStreamVersion expectedVersion, CancellationToken cancellationToken = default);
}
