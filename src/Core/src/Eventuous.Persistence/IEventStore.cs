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
    Task<bool> StreamExists(StreamName stream, CancellationToken cancellationToken);
    
    // /// <summary>
    // /// Read a number of events from a given stream, backwards (from the stream end)
    // /// </summary>
    // /// <param name="stream">Stream name</param>
    // /// <param name="count">How many events to read</param>
    // /// <param name="cancellationToken">Cancellation token</param>
    // /// <returns>An array with events retrieved from the stream</returns>
    // Task<StreamEvent[]> ReadEventsBackwards(
    //     StreamName        stream,
    //     int               count,
    //     CancellationToken cancellationToken
    // );

    /// <summary>
    /// Truncate a stream at a given position
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="truncatePosition">Where to truncate the stream</param>
    /// <param name="expectedVersion">Expected stream version (could be Any)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task TruncateStream(
        StreamName             stream,
        StreamTruncatePosition truncatePosition,
        ExpectedStreamVersion  expectedVersion,
        CancellationToken      cancellationToken
    );

    /// <summary>
    /// Delete a stream
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="expectedVersion">Expected stream version (could be Any)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns></returns>
    Task DeleteStream(
        StreamName            stream,
        ExpectedStreamVersion expectedVersion,
        CancellationToken     cancellationToken
    );
}