namespace Eventuous.Subscriptions;

public class NoOpGapMeasure : ISubscriptionGapMeasure {
    public void PutGap(string subscriptionId, ulong gap, DateTime created) { }

    public SubscriptionGap GetGap(string subscriptionId) => Dummy;

    static readonly SubscriptionGap Dummy = new(0, TimeSpan.Zero);
}