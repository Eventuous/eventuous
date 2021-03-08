using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.EventStoreDB.Subscriptions {
    [PublicAPI]
    public class AllStreamSubscriptionService : SubscriptionService {
        readonly IEventFilter _eventFilter;

        protected AllStreamSubscriptionService(
            EventStoreClient           eventEventStoreClient,
            string                     subscriptionName,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> projections,
            ILoggerFactory?            loggerFactory = null,
            IEventFilter?              eventFilter   = null,
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
            => _eventFilter = eventFilter ?? EventTypeFilter.ExcludeSystemEvents();

        protected override ulong? GetPosition(ResolvedEvent resolvedEvent)
            => resolvedEvent.Event.Position.CommitPosition;

        protected override Task<StreamSubscription> Subscribe(
            Checkpoint checkpoint, CancellationToken cancellationToken
        )
            => checkpoint.Position != null
                ? EventStoreClient.SubscribeToAllAsync(
                    new Position(checkpoint.Position.Value, checkpoint.Position.Value),
                    Handler,
                    false,
                    Dropped,
                    new SubscriptionFilterOptions(
                        _eventFilter,
                        10,
                        (_, p, ct) => StoreCheckpoint(p.CommitPosition, ct)
                    ),
                    cancellationToken: cancellationToken
                )
                : EventStoreClient.SubscribeToAllAsync(
                    Handler,
                    false,
                    Dropped,
                    new SubscriptionFilterOptions(
                        _eventFilter,
                        10,
                        (_, p, ct) => StoreCheckpoint(p.CommitPosition, ct)
                    ),
                    cancellationToken: cancellationToken
                );
    }
}