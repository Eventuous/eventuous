using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    /// <summary>
    /// Catch-up subscription for EventStoreDB, for a specific stream
    /// </summary>
    [PublicAPI]
    public class StreamSubscription : EsdbSubscriptionService {
        readonly string _streamName;

        /// <summary>
        /// Creates EventStoreDB catch-up subscription service for a given stream
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamSubscription(
            EventStoreClient           eventStoreClient,
            string                     streamName,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(
            eventStoreClient,
            subscriptionId,
            checkpointStore,
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        )
            => _streamName = Ensure.NotEmptyString(streamName, nameof(streamName));

        /// <summary>
        /// Creates EventStoreDB catch-up subscription service for a given stream
        /// </summary>
        /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamSubscription(
            EventStoreClientSettings   clientSettings,
            string                     streamName,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : this(
            new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))),
            streamName,
            subscriptionId,
            checkpointStore,
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        ) { }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var sub = checkpoint.Position == null
                ? await EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    HandleEvent,
                    true,
                    HandleDrop,
                    cancellationToken: cancellationToken
                )
                : await EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    StreamPosition.FromInt64((long) checkpoint.Position),
                    HandleEvent,
                    true,
                    HandleDrop,
                    cancellationToken: cancellationToken
                );

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            Task HandleEvent(EventStore.Client.StreamSubscription _, ResolvedEvent re, CancellationToken ct)
                => Handler(AsReceivedEvent(re), ct);

            void HandleDrop(EventStore.Client.StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
                => Dropped(EsdbMappings.AsDropReason(reason), ex);

            static ReceivedEvent AsReceivedEvent(ResolvedEvent re)
                => new() {
                    EventId        = re.Event.EventId.ToString(),
                    GlobalPosition = re.Event.Position.CommitPosition,
                    StreamPosition = re.Event.EventNumber,
                    OriginalStream = re.OriginalStreamId,
                    Sequence       = re.Event.EventNumber,
                    Created        = re.Event.Created,
                    EventType      = re.Event.EventType,
                    Data           = re.Event.Data,
                    Metadata       = re.Event.Metadata
                };
        }
    }
}