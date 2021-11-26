namespace Eventuous.Subscriptions.Diagnostics;

public delegate ValueTask<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken);

[PublicAPI]
public record struct SubscriptionGap(string SubscriptionId, ulong PositionGap, TimeSpan TimeGap) {
    public static readonly SubscriptionGap Invalid = new("error", 0, TimeSpan.Zero);
}