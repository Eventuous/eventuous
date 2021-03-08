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
            EventStoreClient           eventEventStoreClient,
            string                     streamName,
            string                     subscriptionName,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> projections,
            ILoggerFactory?            loggerFactory = null,
            ProjectionGapMeasure?      measure       = null
        ) : base(
            eventEventStoreClient,
            subscriptionName,
            checkpointStore,
            eventSerializer,
            projections,
            loggerFactory,
            measure
        )
            => _streamName = streamName;

        protected override ulong? GetPosition(ResolvedEvent resolvedEvent)
            => resolvedEvent.Event.EventNumber.ToUInt64();

        protected override Task<StreamSubscription> Subscribe(
            Checkpoint checkpoint, CancellationToken cancellationToken
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