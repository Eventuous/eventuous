using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.GooglePubSub {
    [PublicAPI]
    public class GooglePubSubSubscription : SubscriptionService, ICanStop {
        readonly SubscriptionName _subscriptionName;
        readonly SubscriberClient _client;

        public GooglePubSubSubscription(
            string                     projectId,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(subscriptionId, new NoOpCheckpointStore(), eventSerializer, eventHandlers, loggerFactory, measure) {
            _subscriptionName = SubscriptionName.FromProjectSubscription(
                Ensure.NotEmptyString(projectId, nameof(projectId)),
                Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId))
            );

            _client = SubscriberClient.Create(_subscriptionName);
        }

        Task _subscriberTask;

        protected override Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            _subscriberTask = _client.StartAsync(Handle);
            return Task.FromResult(new EventSubscription(SubscriptionId, this));

            async Task<SubscriberClient.Reply> Handle(PubsubMessage msg, CancellationToken ct) {
                var receivedEvent = new ReceivedEvent {
                    Created     = msg.PublishTime.ToDateTime(),
                    Data        = msg.Data.ToByteArray(),
                    ContentType = msg.Attributes["contentType"],
                    EventType   = msg.Attributes["eventType"],
                    EventId     = msg.MessageId,
                    Position    = 0,
                    Sequence    = 0
                };

                try {
                    await Handler(receivedEvent, ct);
                    return SubscriberClient.Reply.Ack;
                }
                catch (Exception) {
                    return SubscriberClient.Reply.Nack;
                }
            }
        }

        protected override Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            // Unsure how to do it properly, need to know how to read from the end of the subscription
            return Task.FromResult(new EventPosition(0, DateTime.Now));
        }

        public async Task Stop(CancellationToken cancellationToken = default) {
            await _client.StopAsync(cancellationToken);
            await _subscriberTask;
        }
    }
}