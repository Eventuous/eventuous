using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreSubscriptionBase<T> : EventSubscription<T>
    where T : EventStoreSubscriptionOptions {
    readonly IMetadataSerializer _metaSerializer;

    protected EventStoreClient EventStoreClient { get; }

    protected EventStoreSubscriptionBase(
        EventStoreClient eventStoreClient,
        T                options,
        IMessageConsumer consumer,
        ILoggerFactory?  loggerFactory = null
    ) : base(options, consumer, loggerFactory) {
        EventStoreClient = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
        _metaSerializer  = Options.MetadataSerializer ?? DefaultMetadataSerializer.Instance;
    }

    protected Metadata? DeserializeMeta(
        ReadOnlyMemory<byte> meta,
        string               stream,
        ulong                position = 0
    ) {
        if (meta.IsEmpty) return null;

        try {
            return _metaSerializer.Deserialize(meta.Span);
        }
        catch (Exception e) {
            Log?.LogError(
                e,
                "Error deserializing metadata {Stream} {Position}",
                stream,
                position
            );

            if (Options.ThrowOnError)
                throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }
    
    protected EventPosition? LastProcessed { get; set; }
}