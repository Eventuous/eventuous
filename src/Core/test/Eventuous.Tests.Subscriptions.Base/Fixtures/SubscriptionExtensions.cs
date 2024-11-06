using Eventuous.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Eventuous.Tests.Subscriptions.Base;

public static class SubscriptionExtensions {
    public static ValueTask SubscribeWithLog(this IMessageSubscription subscription, ILogger log, CancellationToken cancellationToken = default)
        => subscription.Subscribe(
            id => log.LogInformation("{Subscription} subscribed", id),
            (id, reason, ex) => log.LogWarning(ex, "{Subscription} dropped {Reason}", id, reason),
            cancellationToken
        );

    public static ValueTask UnsubscribeWithLog(this IMessageSubscription subscription, ILogger log, CancellationToken cancellationToken = default)
        => subscription.Unsubscribe(
            id => log.LogInformation("{Subscription} unsubscribed", id),
            cancellationToken
        );
}