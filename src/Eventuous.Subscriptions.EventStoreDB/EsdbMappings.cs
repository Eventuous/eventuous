using System;
using EventStore.Client;
using Eventuous.Subscriptions;

namespace Eventuous.Subscriptions.EventStoreDB {
    static class EsdbMappings {
        public static DropReason AsDropReason(SubscriptionDroppedReason reason)
            => reason switch {
                SubscriptionDroppedReason.Disposed => DropReason.Stopped,
                SubscriptionDroppedReason.ServerError => DropReason.ServerError,
                SubscriptionDroppedReason.SubscriberError => DropReason.SubscriptionError,
                _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null)
            };

        public static ReceivedMessage ToMessageReceived(this ResolvedEvent re)
            => new() {
                MessageId   = re.Event.EventId.ToGuid(),
                Position    = re.Event.Position.CommitPosition,
                Sequence    = re.Event.EventNumber,
                Created     = re.Event.Created,
                MessageType = re.Event.EventType,
                Data        = re.Event.Data,
                Metadata    = re.Event.Metadata
            };
    }
}