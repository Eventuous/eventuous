using System;

namespace Eventuous.Subscriptions {
    public interface IReportHealth {
        string             SubscriptionId { get; }
        SubscriptionHealth Health         { get; }
    }

    public record SubscriptionHealth(bool IsHealthy, Exception? LastException);
}