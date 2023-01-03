namespace Eventuous; 

public interface IEventReader {
    /// <summary>
    /// Read a fixed number of events from an existing stream to an array
    /// </summary>
    /// <param name="stream">Stream name</param>
    /// <param name="start">Where to start reading events</param>
    /// <param name="count">How many events to read</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>An array with events retrieved from the stream</returns>
    Task<StreamEvent[]> ReadEvents(
        StreamName         stream,
        StreamReadPosition start,
        int                count,
        CancellationToken  cancellationToken
    );
}
