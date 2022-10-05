using Eventuous.Subscriptions.Filters;
using Eventuous.Subscriptions.Logging;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.EventStore.Subscriptions;

[PublicAPI]
public abstract class EventStoreSubscriptionBase1<T> : EventSubscription<T>
    where T : EventStoreSubscriptionOptions {
    readonly IMetadataSerializer _metaSerializer;

    protected EventStoreClient EventStoreClient { get; }

    protected EventStoreSubscriptionBase1(EventStoreClient eventStoreClient, T options, ConsumePipe consumePipe)
        : base(options, consumePipe) {
        EventStoreClient = Ensure.NotNull(eventStoreClient);
        _metaSerializer  = Options.MetadataSerializer ?? DefaultMetadataSerializer.Instance;
    }

    protected Metadata? DeserializeMeta1(ReadOnlyMemory<byte> meta, string stream, ulong position = 0) {
        if (meta.IsEmpty) return null;

        try {
            return _metaSerializer.Deserialize(meta.Span);
        }
        catch (Exception e) {
            Log.MetadataDeserializationFailed(stream, position, e);

            if (Options.ThrowOnError)
                throw new DeserializationException(stream, "metadata", position, e);

            return null;
        }
    }

    protected EventPosition? LastProcessed { get; set; }
}