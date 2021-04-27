using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class StreamPersistentSubscriptionService : EsdbSubscriptionService {
        readonly EventStorePersistentSubscriptionsClient _persistentSubscriptionsClient;
        readonly string                                  _stream;

        public StreamPersistentSubscriptionService(
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

        public StreamPersistentSubscriptionService(
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