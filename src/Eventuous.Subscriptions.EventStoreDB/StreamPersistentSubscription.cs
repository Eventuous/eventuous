using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    /// <summary>
    /// Persistent subscription for EventStoreDB, for a specific stream
    /// </summary>
    [PublicAPI]
    public class StreamPersistentSubscription : EsdbSubscriptionService {
        readonly EventStorePersistentSubscriptionsClient _persistentSubscriptionsClient;
        readonly string                                  _stream;

        /// <summary>
        /// Creates EventStoreDB persistent subscription service for a given stream
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamPersistentSubscription(
            EventStoreClient                        eventStoreClient,
            EventStorePersistentSubscriptionsClient persistentSubscriptionsClient,
            string                                  streamName,
            string                                  subscriptionId,
            IEventSerializer                        eventSerializer,
            IEnumerable<IEventHandler>              eventHandlers,
            ILoggerFactory?                         loggerFactory = null,
            SubscriptionGapMeasure?                 measure       = null
        ) : base(
            eventStoreClient,
            subscriptionId,
            new NoOpCheckpointStore(),
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        ) {
            _persistentSubscriptionsClient = persistentSubscriptionsClient;
            _stream                        = streamName;
        }

        /// <summary>
        /// Creates EventStoreDB persistent subscription service for a given stream
        /// </summary>
        /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamPersistentSubscription(
            EventStoreClientSettings   clientSettings,
            string                     streamName,
            string                     subscriptionId,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : this(
            new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))),
            new EventStorePersistentSubscriptionsClient(clientSettings),
            streamName,
            subscriptionId,
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        ) { }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        _,
            CancellationToken cancellationToken
        ) {
            var settings = new PersistentSubscriptionSettings(true);

            PersistentSubscription sub;

            try {
                sub = await LocalSubscribe();
            }
            catch (PersistentSubscriptionNotFoundException) {
                await _persistentSubscriptionsClient.CreateAsync(
                    _stream,
                    SubscriptionId,
                    settings,
                    cancellationToken: cancellationToken
                );

                sub = await LocalSubscribe();
            }

            return new EventSubscription(SubscriptionId, new Stoppable(() => sub.Dispose()));

            void HandleDrop(PersistentSubscription __, SubscriptionDroppedReason reason, Exception? exception)
                => Dropped(EsdbMappings.AsDropReason(reason), exception);

            async Task HandleEvent(
                PersistentSubscription subscription,
                ResolvedEvent          re,
                int?                   retryCount,
                CancellationToken      ct
            ) {
                try {
                    await Handler(AsReceivedEvent(re), ct);
                    await subscription.Ack(re);
                }
                catch (Exception e) {
                    await subscription.Nack(PersistentSubscriptionNakEventAction.Retry, e.Message, re);
                }
            }

            Task<PersistentSubscription> LocalSubscribe()
                => _persistentSubscriptionsClient.SubscribeAsync(
                    _stream,
                    SubscriptionId,
                    HandleEvent,
                    HandleDrop,
                    cancellationToken: cancellationToken
                );
            
            static ReceivedEvent AsReceivedEvent(ResolvedEvent re)
                => new() {
                    EventId        = re.Event.EventId.ToString(),
                    GlobalPosition = re.Event.Position.CommitPosition,
                    OriginalStream = re.OriginalStreamId,
                    StreamPosition = re.Event.EventNumber,
                    Sequence       = re.Event.EventNumber,
                    Created        = re.Event.Created,
                    EventType      = re.Event.EventType,
                    Data           = re.Event.Data,
                    Metadata       = re.Event.Metadata
                };
        }
    }
}