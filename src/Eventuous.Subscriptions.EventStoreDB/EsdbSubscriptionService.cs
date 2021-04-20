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
            EventStoreClient = eventStoreClient;
        }

        
        protected Task Handler(StreamSubscription _, ResolvedEvent re, CancellationToken cancellationToken)
            => Handler(re.ToMessageReceived(), cancellationToken);

        protected void Dropped(
            StreamSubscription sub,
            SubscriptionDroppedReason reason,
            Exception?                exception
        ) {
            if (!IsRunning) return;

            _log.LogWarning(
                exception,
                "Subscription {Subscription} dropped {Reason}",
                Subscription.SubscriptionId,
                reason
            );

            IsDropped = true;

            Task.Run(
                () => Resubscribe(
                    reason == SubscriptionDroppedReason.Disposed ? TimeSpan.FromSeconds(10) : TimeSpan.Zero
                )
            );
        }
        
        protected override async Task<MessagePosition> GetLastEventPosition(CancellationToken cancellationToken) {
            var read = EventStoreClient.ReadAllAsync(Direction.Backwards, Position.End, 1, cancellationToken: cancellationToken);
            var events = await read.ToArrayAsync(cancellationToken);
            return new MessagePosition(events[0].Event.Position.CommitPosition, events[0].Event.Created);
        }
    }
}