namespace Eventuous.Subscriptions; 

public record SubscriptionOptions {
    /// <summary>
    /// Subscription id is used to match event handlers with one subscription
    /// </summary>
    public string SubscriptionId { get; init; } = null!;
        
    /// <summary>
    /// Set to true if you want the subscription to fail and stop if anything goes wrong.
    /// </summary>
    public bool ThrowOnError { get; init; }
    
    /// <summary>
    /// Custom event serializer. If not assigned, the default serializer will be used.
    /// </summary>
    public IEventSerializer? EventSerializer { get; set; }
}