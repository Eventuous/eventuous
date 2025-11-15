using Eventuous.Subscriptions;
using Microsoft.Extensions.Logging;

namespace Eventuous.Tests.Subscriptions.Base;

public static class SubscriptionExtensions {
    extension(IMessageSubscription subscription) {
        public ValueTask SubscribeWithLog(ILogger log, CancellationToken cancellationToken = default)
            => subscription.Subscribe(
                id => log.LogInformation("{Subscription} subscribed", id),
                (id, reason, ex) => log.LogWarning(ex, "{Subscription} dropped {Reason}", id, reason),
                cancellationToken
            );

        public ValueTask UnsubscribeWithLog(ILogger log, CancellationToken cancellationToken = default)
            => subscription.Unsubscribe(
                id => log.LogInformation("{Subscription} unsubscribed", id),
                cancellationToken
            );
    }
}