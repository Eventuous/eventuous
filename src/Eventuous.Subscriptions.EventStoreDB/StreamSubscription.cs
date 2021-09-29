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
    public class StreamSubscription : EventStoreSubscriptionService {
        readonly StreamSubscriptionOptions _options;

        /// <summary>
        /// Creates EventStoreDB catch-up subscription service for a given stream
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="metaSerializer"></param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        /// <param name="throwOnError"></param>
        public StreamSubscription(
            EventStoreClient           eventStoreClient,
            string                     streamName,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            IMetadataSerializer?       metaSerializer  = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null,
            bool                       throwOnError    = false
        ) : this(
            eventStoreClient,
            new StreamSubscriptionOptions {
                StreamName     = streamName,
                SubscriptionId = subscriptionId,
                ThrowOnError   = throwOnError
            },
            checkpointStore,
            eventHandlers,
            eventSerializer,
            metaSerializer,
            loggerFactory,
            measure
        ) { }

        /// <summary>
        /// Creates EventStoreDB catch-up subscription service for a given stream
        /// </summary>
        /// <param name="client"></param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="options">Subscription options</param>
        /// <param name="metaSerializer"></param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamSubscription(
            EventStoreClient           client,
            StreamSubscriptionOptions  options,
            ICheckpointStore           checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            IMetadataSerializer?       metaSerializer  = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) : base(
            client,
            options,
            checkpointStore,
            eventHandlers,
            eventSerializer,
            metaSerializer,
            loggerFactory,
            measure
        ) {
            Ensure.NotEmptyString(options.StreamName, nameof(options.StreamName));

            _options = options;
        }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var subTask = checkpoint.Position == null
                ? EventStoreClient.SubscribeToStreamAsync(
                    _options.StreamName,
                    HandleEvent,
                    _options.ResolveLinkTos,
                    HandleDrop,
                    _options.ConfigureOperation,
                    _options.Credentials,
                    cancellationToken
                )
                : EventStoreClient.SubscribeToStreamAsync(
                    _options.StreamName,
                    StreamPosition.FromInt64((long)checkpoint.Position),
                    HandleEvent,
                    _options.ResolveLinkTos,
                    HandleDrop,
                    _options.ConfigureOperation,
                    _options.Credentials,
                    cancellationToken
                );

            var sub = await subTask.NoContext();

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            async Task HandleEvent(
                EventStore.Client.StreamSubscription _,
                ResolvedEvent                        re,
                CancellationToken                    ct
            ) {
                // Despite ResolvedEvent.Event being not marked as nullable, it returns null for deleted events
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (re.Event is null) return;

                await Handler(AsReceivedEvent(re), ct).NoContext();
            }

            void HandleDrop(
                EventStore.Client.StreamSubscription _,
                SubscriptionDroppedReason            reason,
                Exception?                           ex
            )
                => Dropped(EsdbMappings.AsDropReason(reason), ex);

            ReceivedEvent AsReceivedEvent(ResolvedEvent re) {
                var evt = DeserializeData(
                    re.Event.ContentType,
                    re.Event.EventType,
                    re.Event.Data,
                    re.Event.EventStreamId,
                    re.Event.EventNumber
                );

                return new ReceivedEvent(
                    re.Event.EventId.ToString(),
                    re.Event.EventType,
                    re.Event.ContentType,
                    re.Event.Position.CommitPosition,
                    re.Event.EventNumber,
                    re.Event.EventStreamId,
                    re.OriginalEventNumber.ToUInt64(),
                    re.Event.Created,
                    evt,
                    DeserializeMeta(re.Event.Metadata, re.OriginalStreamId, re.Event.EventNumber)
                );
            }
        }
    }
}
