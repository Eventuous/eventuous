using Eventuous.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Eventuous.Sut.Subs;

public static class SubscriptionExtensions {
    public static ValueTask SubscribeWithLog(this IMessageSubscription subscription, ILogger log)
        => subscription.Subscribe(
            id => log.LogInformation("{Subscription} subscribed", id),
            (id, reason, ex) => log.LogWarning(ex, "{Subscription} dropped {Reason}", id, reason),
            CancellationToken.None
        );

    public static ValueTask UnsubscribeWithLog(this IMessageSubscription subscription, ILogger log)
        => subscription.Unsubscribe(
            id => log.LogInformation("{Subscription} unsubscribed", id),
            CancellationToken.None
        );
}