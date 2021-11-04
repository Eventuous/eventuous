namespace Eventuous.Subscriptions.Diagnostics;

public delegate ValueTask<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken);

[PublicAPI]
public record SubscriptionGap(string SubscriptionId, ulong PositionGap, TimeSpan TimeGap);