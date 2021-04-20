using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eventuous.Subscriptions {
    /// <summary>
    /// The gap measurement tool, which can be used for metrics and alerts when the subscription
    /// is lagging behind real-time updates.
    /// </summary>
    [PublicAPI]
    public class SubscriptionGapMeasure {
        readonly Dictionary<string, SubscriptionGap> _gaps = new();

        public void PutGap(string subscriptionId, ulong gap, DateTime created)
            => _gaps[subscriptionId] = new SubscriptionGap(gap, DateTime.Now - created);

        /// <summary>
        /// Retrieve the current subscription gap
        /// </summary>
        /// <param name="subscriptionId">Subscription identifier</param>
        /// <returns></returns>
        public SubscriptionGap GetGap(string subscriptionId) => _gaps[subscriptionId];
    }

    public record SubscriptionGap(ulong PositionGap, TimeSpan TimeGap);
}