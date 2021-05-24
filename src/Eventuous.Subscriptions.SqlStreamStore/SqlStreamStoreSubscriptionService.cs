using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Eventuous;
using Eventuous.Subscriptions;
using SqlStreamStore;
using SqlStreamStore.Streams;

namespace Eventuous.Subscriptions.SqlStreamStore
{
    [PublicAPI]
    public abstract class SqlStreamStoreSubscriptionService : SubscriptionService {
        protected IStreamStore StreamStore { get; }

        protected SqlStreamStoreSubscriptionService(
            IStreamStore streamStore,
            SqlStreamStoreSubscriptionOptions options,
            ICheckpointStore checkpointStore,
            IEnumerable<IEventHandler> eventHandlers,
            IEventSerializer? eventSerializer = null,
            ILoggerFactory?  loggerFactory = null,
            ISubscriptionGapMeasure? measure = null
        ) : base(options, checkpointStore, eventHandlers, eventSerializer, loggerFactory, measure) {
            StreamStore = Ensure.NotNull(streamStore, nameof(streamStore));
        }

        protected override async Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            var page = await StreamStore.ReadAllBackwards(
                Position.End,
                1,
                true,
                cancellationToken
            );
            return new EventPosition((ulong) page.Messages[0].Position, page.Messages[0].CreatedUtc);
        }
    }
}
