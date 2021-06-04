using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SqlStreamStore;
using SqlStreamStore.Streams;
using Eventuous.Subscriptions.SqlStreamStore;

namespace Eventuous.Subscriptions.SqlStreamStore.MsSql
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io) which has a MsSQL database as the event data store.
    /// </summary>
    [PublicAPI]
    public class StreamSubscription : SqlStreamStore.StreamSubscription
    {
        /// <summary>
        /// Creates SqlStreamStore catch-up subscription service for $all
        /// </summary>
        /// <param name="streamStore">SqlStreamStore instance</param>
        /// <param name="subscriptionId">Subscription ID</param>
        /// <param name="checkpointStore">Checkpoint store instance</param>
        /// <param name="eventHandlers">Collection of event handlers</param>
        /// <param name="eventSerializer">Event serializer instance</param>
        /// <param name="loggerFactory">Optional: logger factory</param>
        /// <param name="measure">Optional: gap measurement for metrics</param>

        public StreamSubscription(
            MsSqlStreamStoreV3          msSqlStore,
            string                      streamName,
            string                      subscriptionId,
            ICheckpointStore            checkpointStore,
            IEnumerable<IEventHandler>  eventHandlers,
            IEventSerializer?           eventSerializer = null,
            ILoggerFactory?             loggerFactory   = null,
            ISubscriptionGapMeasure?    measure         = null,
            bool                        throwOnError    = false
        ) : base(
            Ensure.NotNull(msSqlStore, nameof(msSqlStore)),
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
