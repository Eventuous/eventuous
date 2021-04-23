using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class StreamSubscriptionService : EsdbSubscriptionService {
        readonly string _streamName;

        public StreamSubscriptionService(
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

        public StreamSubscriptionService(
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

            return new EventSubscription(SubscriptionId, sub);
            
            Task HandleEvent(StreamSubscription _, ResolvedEvent re, CancellationToken ct)
                => Handler(re.ToMessageReceived(), ct);

            void HandleDrop(StreamSubscription _, SubscriptionDroppedReason reason, Exception? ex)
                => Dropped(EsdbMappings.AsDropReason(reason), ex);
        }
    }
}