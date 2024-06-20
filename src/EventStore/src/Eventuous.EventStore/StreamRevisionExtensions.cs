namespace Eventuous.EventStore;

/// <summary>
/// Internal conversions between Event Store and Eventuous types for stream positions and revisions
/// </summary>
public static class StreamRevisionExtensions {
    /// <summary>
    /// Converts <see cref="StreamRevision"/> to <see cref="ExpectedStreamVersion"/>
    /// </summary>
    /// <param name="version">Stream version</param>
    /// <returns></returns>
    public static StreamRevision AsStreamRevision(this ExpectedStreamVersion version) => StreamRevision.FromInt64(version.Value);

    /// <summary>
    /// Converts <see cref="StreamTruncatePosition"/> to <see cref="ExpectedStreamVersion"/>
    /// </summary>
    /// <param name="position">Position for stream truncation</param>
    /// <returns></returns>
    public static StreamPosition AsStreamPosition(this StreamTruncatePosition position) => StreamPosition.FromInt64(position.Value);

    /// <summary>
    /// Converts <see cref="StreamReadPosition"/> to <see cref="StreamPosition"/>
    /// </summary>
    /// <param name="position">Position for stream reads</param>
    /// <returns></returns>
    public static StreamPosition AsStreamPosition(this StreamReadPosition position) => StreamPosition.FromInt64(position.Value);
}
