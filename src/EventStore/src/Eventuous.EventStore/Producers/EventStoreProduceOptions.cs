namespace Eventuous.EventStore.Producers; 

[PublicAPI]
public record EventStoreProduceOptions {
    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; init; }
        
    /// <summary>
    /// Expected stream state
    /// </summary>
    public StreamState ExpectedState { get; init; } = StreamState.Any;

    /// <summary>
    /// Maximum number of events appended to a single stream in one batch
    /// </summary>
    public int MaxAppendEventsCount { get; init; } = 500;
    
    public TimeSpan? Deadline { get; init; }

    public static EventStoreProduceOptions Default { get; } = new();
}