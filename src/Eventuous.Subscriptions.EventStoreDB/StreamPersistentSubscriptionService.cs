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
        PersistentSubscription                           _subscription = null!;

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

            try {
                await LocalSubscribe();
            }
            catch (PersistentSubscriptionNotFoundException) {
                await _persistentSubscriptionsClient.CreateAsync(
                    _stream,
                    SubscriptionId,
                    settings,
                    cancellationToken: cancellationToken
                );

                await LocalSubscribe();
            }

            return new EventSubscription(SubscriptionId, new Disposable(Close));

            void HandleDrop(PersistentSubscription sub, SubscriptionDroppedReason reason, Exception? exception)
                => Dropped(EsdbMappings.AsDropReason(reason), exception);

            Task HandleEvent(
                PersistentSubscription sub,
                ResolvedEvent          re,
                int?                   retryCount,
                CancellationToken      ct
            )
                => Handler(re.ToMessageReceived(), ct);

            async Task LocalSubscribe() {
                _subscription = await _persistentSubscriptionsClient.SubscribeAsync(
                    _stream,
                    SubscriptionId,
                    HandleEvent,
                    HandleDrop,
                    cancellationToken: cancellationToken
                );
            }
        }

        void Close() => _subscription.Dispose();
    }
}