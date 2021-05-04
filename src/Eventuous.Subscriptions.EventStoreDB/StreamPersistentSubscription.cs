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
    public class StreamPersistentSubscription : EventStoreSubscriptionService {
        readonly EventStorePersistentSubscriptionsClient  _subscriptionClient;
        readonly string                                   _stream;
        readonly EventStorePersistentSubscriptionOptions? _options;

        /// <summary>
        /// Creates EventStoreDB persistent subscription service for a given stream
        /// </summary>
        /// <param name="eventStoreClient">EventStoreDB gRPC client instance</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="options">Subscription options</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        /// <param name="subscriptionClient">Client for EventStoreDB persistent subscriptions</param>
        public StreamPersistentSubscription(
            EventStoreClient                         eventStoreClient,
            EventStorePersistentSubscriptionsClient  subscriptionClient,
            string                                   streamName,
            string                                   subscriptionId,
            IEventSerializer                         eventSerializer,
            IEnumerable<IEventHandler>               eventHandlers,
            EventStorePersistentSubscriptionOptions? options       = null,
            ILoggerFactory?                          loggerFactory = null,
            SubscriptionGapMeasure?                  measure       = null
        ) : base(
            eventStoreClient,
            subscriptionId,
            new NoOpCheckpointStore(),
            eventSerializer,
            eventHandlers,
            loggerFactory,
            measure
        ) {
            _subscriptionClient = subscriptionClient;
            _stream             = streamName;
            _options            = options;
        }

        /// <summary>
        /// Creates EventStoreDB persistent subscription service for a given stream
        /// </summary>
        /// <param name="clientSettings">EventStoreDB gRPC client settings</param>
        /// <param name="streamName">Name of the stream to receive events from</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="options">Subscription options</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>
        public StreamPersistentSubscription(
            EventStoreClientSettings                 clientSettings,
            string                                   streamName,
            string                                   subscriptionId,
            IEventSerializer                         eventSerializer,
            IEnumerable<IEventHandler>               eventHandlers,
            EventStorePersistentSubscriptionOptions? options       = null,
            ILoggerFactory?                          loggerFactory = null,
            SubscriptionGapMeasure?                  measure       = null
        ) : this(
            new EventStoreClient(Ensure.NotNull(clientSettings, nameof(clientSettings))),
            new EventStorePersistentSubscriptionsClient(clientSettings),
            streamName,
            subscriptionId,
            eventSerializer,
            eventHandlers,
            options,
            loggerFactory,
            measure
        ) { }

        protected override async Task<EventSubscription> Subscribe(
            Checkpoint        _,
            CancellationToken cancellationToken
        ) {
            var settings = _options?.SubscriptionSettings ?? new PersistentSubscriptionSettings(true);
            var autoAck  = _options?.AutoAck ?? true;

            PersistentSubscription sub;

            try {
                sub = await LocalSubscribe();
            }
            catch (PersistentSubscriptionNotFoundException) {
                await _subscriptionClient.CreateAsync(
                    _stream,
                    SubscriptionId,
                    settings,
                    _options?.Credentials,
                    cancellationToken
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

                    if (!autoAck)
                        await subscription.Ack(re);
                }
                catch (Exception e) {
                    await subscription.Nack(PersistentSubscriptionNakEventAction.Retry, e.Message, re);
                }
            }

            Task<PersistentSubscription> LocalSubscribe()
                => _subscriptionClient.SubscribeAsync(
                    _stream,
                    SubscriptionId,
                    HandleEvent,
                    HandleDrop,
                    _options?.Credentials,
                    _options?.BufferSize ?? 10,
                    _options?.AutoAck ?? true,
                    cancellationToken
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