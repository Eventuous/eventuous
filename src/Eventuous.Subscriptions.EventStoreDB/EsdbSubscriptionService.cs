using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Eventuous.Subscriptions.EventStoreDB {
    [PublicAPI]
    public abstract class EsdbSubscriptionService : SubscriptionService {
        readonly SubscriptionGapMeasure? _measure;
        readonly ILogger?                _log;
        readonly Log?                    _debugLog;

        protected EventStoreClient EventStoreClient { get; }

        protected EsdbSubscriptionService(
            EventStoreClient           eventStoreClient,
            string                     subscriptionId,
            ICheckpointStore           checkpointStore,
            IEventSerializer           eventSerializer,
            IEnumerable<IEventHandler> eventHandlers,
            ILoggerFactory?            loggerFactory = null,
            SubscriptionGapMeasure?    measure       = null
        ) : base(subscriptionId, checkpointStore, eventSerializer, eventHandlers, loggerFactory, measure) {
            EventStoreClient = Ensure.NotNull(eventStoreClient, nameof(eventStoreClient));
        }

        protected override async Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            var read = EventStoreClient.ReadAllAsync(
                Direction.Backwards,
                Position.End,
                1,
                cancellationToken: cancellationToken
            );

            var events = await read.ToArrayAsync(cancellationToken);
            return new EventPosition(events[0].Event.Position.CommitPosition, events[0].Event.Created);
        }
    }
}