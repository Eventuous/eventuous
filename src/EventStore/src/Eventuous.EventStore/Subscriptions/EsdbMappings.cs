namespace Eventuous.EventStore.Subscriptions; 

static class EsdbMappings {
    public static DropReason AsDropReason(SubscriptionDroppedReason reason)
        => reason switch {
            SubscriptionDroppedReason.Disposed => DropReason.Stopped,
            SubscriptionDroppedReason.ServerError => DropReason.ServerError,
            SubscriptionDroppedReason.SubscriberError => DropReason.SubscriptionError,
            _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
        };

}