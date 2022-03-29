namespace Eventuous.EventStore.Subscriptions;

public record StreamSubscriptionOptions : CatchUpSubscriptionOptions {
    /// <summary>
    /// WHen set to true, all events of type that starts with '$' will be ignored. Default is true.
    /// </summary>
    public bool IgnoreSystemEvents { get; set; } = true;

    /// <summary>
    /// Stream name to subscribe for
    /// </summary>
    public StreamName StreamName { get; set; }
}