using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public abstract class EventStoreSubscriptionService : SubscriptionService {
        protected EventStoreClient EventStoreClient { get; }

        protected EventStoreSubscriptionService(
            EventStoreClient              eventStoreClient,
            EventStoreSubscriptionOptions options,
            ICheckpointStore              checkpointStore,
            IEnumerable<IEventHandler>    eventHandlers,
            IEventSerializer?             eventSerializer = null,
            ILoggerFactory?               loggerFactory   = null,
            ISubscriptionGapMeasure?      measure         = null
        ) : base(options, checkpointStore, eventHandlers, eventSerializer, loggerFactory, measure) {
            EventStoreClient = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
        }

        protected override async Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            var read = EventStoreClient.ReadAllAsync(
                Direction.Backwards,
                Position.End,
                1,
                cancellationToken: cancellationToken
            );

            var events = await read.ToArrayAsync(cancellationToken).NoContext();
            return new EventPosition(events[0].Event.Position.CommitPosition, events[0].Event.Created);
        }
    }
}