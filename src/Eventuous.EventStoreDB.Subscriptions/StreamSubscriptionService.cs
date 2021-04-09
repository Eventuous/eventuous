using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.EventStoreDB.Subscriptions {
    [PublicAPI]
    public class StreamSubscriptionService : SubscriptionService {
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
            => _streamName = streamName;

        protected override ulong? GetPosition(ResolvedEvent resolvedEvent)
            => resolvedEvent.Event.EventNumber.ToUInt64();

        protected override Task<StreamSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        )
            => checkpoint.Position == null
                ? EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    Handler,
                    true,
                    Dropped,
                    cancellationToken: cancellationToken
                )
                : EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    StreamPosition.FromInt64((long) checkpoint.Position),
                    Handler,
                    true,
                    Dropped,
                    cancellationToken: cancellationToken
                );
    }
}