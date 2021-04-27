using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Subscriptions.NoOps;
using Google.Cloud.PubSub.V1;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.GooglePubSub {
    /// <summary>
    /// Google PubSub subscription service
    /// </summary>
    [PublicAPI]
    public class GooglePubSubSubscription : SubscriptionService, ICanStop {
        readonly SubscriptionName _subscriptionName;
        readonly SubscriberClient _client;

        /// <summary>
        /// Creates a Google PubSub subscription service
        /// </summary>
        /// <param name="projectId">GCP project ID</param>
        /// <param name="subscriptionId">Google PubSub subscription ID (within the project), which must already exist</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        public GooglePubSubSubscription(
            string                     projectId,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null
        ) : base(subscriptionId, new NoOpCheckpointStore(), eventSerializer, eventHandlers, loggerFactory, new NoOpGapMeasure()) {
            _subscriptionName = SubscriptionName.FromProjectSubscription(
                Ensure.NotEmptyString(projectId, nameof(projectId)),
                Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId))
            );

            _client = SubscriberClient.Create(_subscriptionName);
        }

        Task _subscriberTask = null!;

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
                    GlobalPosition    = 0,
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