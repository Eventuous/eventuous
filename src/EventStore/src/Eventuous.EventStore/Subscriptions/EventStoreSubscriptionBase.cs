using Eventuous.Subscriptions.Diagnostics;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreSubscriptionBase<T> : EventSubscription<T>
    where T : EventStoreSubscriptionOptions {
    readonly IMetadataSerializer _metaSerializer;

    protected EventStoreClient EventStoreClient { get; }

    protected EventStoreSubscriptionBase(EventStoreClient eventStoreClient, T options, ConsumePipe consumePipe)
        : base(options, consumePipe) {
        EventStoreClient = Ensure.NotNull(eventStoreClient);
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
            SubscriptionsEventSource.Log.MetadataDeserializationFailed(
                Options.SubscriptionId,
                stream,
                position,
                e.ToString()
            );

            if (Options.ThrowOnError)
                throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }

    protected EventPosition? LastProcessed { get; set; }
}