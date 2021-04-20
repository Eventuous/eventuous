using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Client;
using Eventuous.Subscriptions;
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
            => _streamName = streamName;

        protected override async Task<MessageSubscription> Subscribe(
            Checkpoint        checkpoint,
            CancellationToken cancellationToken
        ) {
            var sub = checkpoint.Position == null
                ? await EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    Handler,
                    true,
                    Dropped,
                    cancellationToken: cancellationToken
                )
                : await EventStoreClient.SubscribeToStreamAsync(
                    _streamName,
                    StreamPosition.FromInt64((long) checkpoint.Position),
                    Handler,
                    true,
                    Dropped,
                    cancellationToken: cancellationToken
                );

            return new MessageSubscription(SubscriptionId, sub);
        }
    }
}