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
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamSubscription(
            EventStoreClient           client,
            StreamSubscriptionOptions  options,
            ICheckpointStore           checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer?          eventSerializer = null,
            ILoggerFactory?            loggerFactory   = null,
            ISubscriptionGapMeasure?   measure         = null
        ) : base(client, options, checkpointStore, eventHandlers, eventSerializer, loggerFactory, measure) {
            Ensure.NotEmptyString(options.StreamName, nameof(options.StreamName));

            _options = options;
        }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var sub = checkpoint.Position == null
                ? await EventStoreClient.SubscribeToStreamAsync(
                    _options.StreamName,
                    HandleEvent,
                    _options.ResolveLinkTos,
                    HandleDrop,
                    _options.ConfigureOperation,
                    _options.Credentials,
                    cancellationToken
                )
                : await EventStoreClient.SubscribeToStreamAsync(
                    _options.StreamName,
                    StreamPosition.FromInt64((long) checkpoint.Position),
                    HandleEvent,
                    _options.ResolveLinkTos,
                    HandleDrop,
                    _options.ConfigureOperation,
                    _options.Credentials,
                    cancellationToken
                );

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            async Task HandleEvent(EventStore.Client.StreamSubscription _, ResolvedEvent re, CancellationToken ct) {
                await Handler(AsReceivedEvent(re), ct);
            }

            void HandleDrop(EventStore.Client.StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
                => Dropped(EsdbMappings.AsDropReason(reason), ex);

            static ReceivedEvent AsReceivedEvent(ResolvedEvent re)
                => new() {
                    EventId        = re.Event.EventId.ToString(),
                    GlobalPosition = re.Event.Position.CommitPosition,
                    StreamPosition = re.Event.EventNumber,
                    Stream         = re.Event.EventStreamId,
                    Sequence       = re.Event.EventNumber,
                    Created        = re.Event.Created,
                    EventType      = re.Event.EventType,
                    Data           = re.Event.Data,
                    Metadata       = re.Event.Metadata
                };
        }
    }
}