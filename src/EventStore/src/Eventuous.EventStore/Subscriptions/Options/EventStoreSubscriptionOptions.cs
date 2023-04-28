namespace Eventuous.EventStore.Subscriptions;

/// <summary>
/// Base class for EventStoreDB subscription options
/// </summary>
public abstract record EventStoreSubscriptionOptions : SubscriptionOptions {
    /// <summary>
    /// User credentials
    /// </summary>
    public UserCredentials? Credentials { get; set; }

    /// <summary>
    /// Resolve link events
    /// </summary>
    public bool ResolveLinkTos { get; set; }

    /// <summary>
    /// Metadata serializer. If not assigned, the default one will be used.
    /// </summary>
    public IMetadataSerializer? MetadataSerializer { get; set; }
}