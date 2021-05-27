﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using SqlStreamStore;
using SqlStreamStore.Streams;
using Eventuous.Subscriptions.SqlStreamStore;

namespace Eventuous.Subscriptions.SqlStreamStore.MySql
{
    /// <summary>
    /// Producer for SqlStreamStore (https://sqlstreamstore.readthedocs.io) which has a MySQL database as the event data store.
    /// </summary>
    [PublicAPI]
    public class AllStreamSubscription : SqlStreamStore.AllStreamSubscription
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

        public AllStreamSubscription(
            MySqlStreamStore            mySqlStore,
            string                      subscriptionId,
            ICheckpointStore            checkpointStore,
            IEnumerable<IEventHandler>  eventHandlers,
            IEventSerializer?           eventSerializer = null,
            ILoggerFactory?             loggerFactory   = null,
            ISubscriptionGapMeasure?    measure         = null
        ) : base(
            Ensure.NotNull(mySqlStore, nameof(mySqlStore)),
            new AllStreamSubscriptionOptions { SubscriptionId = subscriptionId},
            checkpointStore,
            eventHandlers,
            eventSerializer,
            loggerFactory,
            measure
        ) { }

    }
}
