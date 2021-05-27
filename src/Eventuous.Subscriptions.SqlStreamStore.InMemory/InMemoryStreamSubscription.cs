using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SqlStreamStore;
using SqlStreamStore.Streams;
using Eventuous.Subscriptions.SqlStreamStore;

namespace Eventuous.Subscriptions.SqlStreamStore.InMemory
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io) with an in-memory event data store.
    /// </summary>
    [PublicAPI]
    public class InMemoryStreamSubscription : StreamSubscription
    {
        /// <summary>
        /// Creates SqlStreamStore catch-up subscription service for $all
        /// </summary>
        /// <param name="streamStore">InMemoryStreamStore instance</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>

        public InMemoryStreamSubscription(
            InMemoryStreamStore         inMemoryStore,
            string                      streamName,
            string                      subscriptionId,
            ICheckpointStore            checkpointStore,
            IEnumerable<IEventHandler>  eventHandlers,
            IEventSerializer?           eventSerializer = null,
            ILoggerFactory?             loggerFactory   = null,
            ISubscriptionGapMeasure?    measure         = null,
            bool                        throwOnError    = false
        ) : base(
            Ensure.NotNull(inMemoryStore, nameof(inMemoryStore)),
            new StreamSubscriptionOptions { 
                StreamName = streamName,
                SubscriptionId = subscriptionId,
                ThrowOnError = throwOnError
            },
            checkpointStore,
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) { }

    }
}
