using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class SubscriptionDrop()
    : SubscriptionDropBase<KurrentDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore>(
        new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            new("$all"),
            false
        )
    ) {
    [Test]
    [Retry(3)]
    public async Task Esdb_ShouldResubscribeAfterConnectionDrop(CancellationToken cancellationToken) {
        await ShouldResubscribeAfterConnectionDrop(cancellationToken);
    }
}
