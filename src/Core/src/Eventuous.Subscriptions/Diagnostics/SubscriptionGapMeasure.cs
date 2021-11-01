namespace Eventuous.Subscriptions.Diagnostics;

[PublicAPI]
public interface ISubscriptionGapMeasure {
    Task<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken);
}

[PublicAPI]
public record SubscriptionGap(ulong PositionGap, TimeSpan TimeGap);