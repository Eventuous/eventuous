namespace Eventuous.Subscriptions.Monitoring; 

[PublicAPI]
public interface ISubscriptionGapMeasure {
    void PutGap(string subscriptionId, ulong gap, DateTime created);

    SubscriptionGap GetGap(string subscriptionId);
}

/// <summary>
/// The gap measurement tool, which can be used for metrics and alerts when the subscription
/// is lagging behind real-time updates.
/// </summary>
[PublicAPI]
public class SubscriptionGapMeasure : ISubscriptionGapMeasure {
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

[PublicAPI]
public record SubscriptionGap(ulong PositionGap, TimeSpan TimeGap);