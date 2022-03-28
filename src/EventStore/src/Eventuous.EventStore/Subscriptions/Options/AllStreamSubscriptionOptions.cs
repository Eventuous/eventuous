namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public record AllStreamSubscriptionOptions : CatchUpSubscriptionOptions {
    /// <summary>
    /// Server-side event filter
    /// </summary>
    public IEventFilter? EventFilter { get; set; }

    /// <summary>
    /// When using the server-side <see cref="EventFilter"/>, the clients still wants to persist the checkpoint
    /// from time to time, to avoid re-reading lots of filtered out events after the restart. Default is 10.
    /// </summary>
    public uint CheckpointInterval { get; set; } = 10;
}