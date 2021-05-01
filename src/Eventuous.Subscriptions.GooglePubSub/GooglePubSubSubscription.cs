using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.Monitoring.V3;
using Google.Cloud.PubSub.V1;
using Google.Protobuf.WellKnownTypes;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.GooglePubSub {
    /// <summary>
    /// Google PubSub subscription service
    /// </summary>
    [PublicAPI]
    public class GooglePubSubSubscription : SubscriptionService, ICanStop {
        readonly SubscriptionName    _subscriptionName;
        readonly SubscriberClient    _client;
        readonly MetricServiceClient _metricClient;

        /// <summary>
        /// Creates a Google PubSub subscription service
        /// </summary>
        /// <param name="projectId">GCP project ID</param>
        /// <param name="subscriptionId">Google PubSub subscription ID (within the project), which must already exist</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Callback for measuring the subscription gap</param>
        public GooglePubSubSubscription(
            string                     projectId,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(
            subscriptionId,
            new NoOpCheckpointStore(),
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        ) {
            _subscriptionName = SubscriptionName.FromProjectSubscription(
                Ensure.NotEmptyString(projectId, nameof(projectId)),
                Ensure.NotEmptyString(subscriptionId, nameof(subscriptionId))
            );

            _client       = SubscriberClient.Create(_subscriptionName);
            _metricClient = MetricServiceClient.Create();

            _undeliveredCountRequest = GetFilteredRequest(PubSubMetricUndeliveredMessagesCount);
            _oldestAgeRequest        = GetFilteredRequest(PubSubMetricOldestUnackedMessageAge);

            ListTimeSeriesRequest GetFilteredRequest(string metric)
                => new() {
                    Name = $"projects/{projectId}",
                    Filter = $"metric.type = \"pubsub.googleapis.com/subscription/{metric}\" "
                           + $"AND resource.label.subscription_id = \"{subscriptionId}\""
                };
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
                    Created        = msg.PublishTime.ToDateTime(),
                    Data           = msg.Data.ToByteArray(),
                    ContentType    = msg.Attributes["contentType"],
                    EventType      = msg.Attributes["eventType"],
                    EventId        = msg.MessageId,
                    GlobalPosition = 0,
                    Sequence       = 0
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

        const string PubSubMetricUndeliveredMessagesCount = "num_undelivered_messages";
        const string PubSubMetricOldestUnackedMessageAge  = "oldest_unacked_message_age";

        readonly ListTimeSeriesRequest _undeliveredCountRequest;
        readonly ListTimeSeriesRequest _oldestAgeRequest;

        protected override async Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            // Subscription metrics are sampled each 60 sec, so we need to use an extended period
            var interval = new TimeInterval {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow - TimeSpan.FromMinutes(2)),
                EndTime   = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            var undelivered = await GetPoint(_undeliveredCountRequest);
            var oldestAge = await GetPoint(_oldestAgeRequest);
            var age = oldestAge == null ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(-oldestAge.Value.Int64Value);

            return new EventPosition((ulong?) undelivered?.Value?.Int64Value, age);

            async Task<Point?> GetPoint(ListTimeSeriesRequest request) {
                request.Interval = interval;

                var result = _metricClient.ListTimeSeriesAsync(request);
                var page   = await result.ReadPageAsync(1, cancellationToken);
                return page.FirstOrDefault()?.Points?.FirstOrDefault();
            }
        }

        public async Task Stop(CancellationToken cancellationToken = default) {
            await _client.StopAsync(cancellationToken);
            await _subscriberTask;
        }
    }
}