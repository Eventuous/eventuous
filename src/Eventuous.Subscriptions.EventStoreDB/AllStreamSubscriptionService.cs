using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public class AllStreamSubscriptionService : EsdbSubscriptionService {
        readonly IEventFilter _eventFilter;

        protected AllStreamSubscriptionService(
            EventStoreClient           eventStoreClient,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            IEventFilter?              eventFilter   = null,
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
            => _eventFilter = eventFilter ?? EventTypeFilter.ExcludeSystemEvents();

        protected override async Task<MessageSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var sub = checkpoint.Position != null
                ? await EventStoreClient.SubscribeToAllAsync(
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
                : await EventStoreClient.SubscribeToAllAsync(
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

            return new MessageSubscription(SubscriptionId, sub);
        }
    }
}