using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eventuous.EventStoreDB.Subscriptions {
    /// <summary>
    /// The gap measurement tool, which can be used for metrics and alerts when the subscription
    /// is lagging behind real-time updates.
    /// </summary>
    [PublicAPI]
    public class SubscriptionGapMeasure {
        readonly Dictionary<string, ulong> _gaps = new();

        internal void PutGap(string subscriptionId, ulong gap) => _gaps[subscriptionId] = gap;

        /// <summary>
        /// Retrieve the current subscription gap
        /// </summary>
        /// <param name="subscriptionId">Subscription identifier</param>
        /// <returns></returns>
        public ulong GetGap(string subscriptionId) => _gaps[subscriptionId];
    }
}