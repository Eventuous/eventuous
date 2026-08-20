using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class SubscriptionRestart()
    : SubscriptionRestartBase<KurrentDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore>(
        new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            new("$all"),
            false
        )
    ) {
    [Test]
    public async Task Esdb_ShouldTolerateRepeatedUnsubscribe() {
        await ShouldTolerateRepeatedUnsubscribe();
    }

    [Test]
    public async Task Esdb_ShouldConsumeAfterResubscribe(CancellationToken cancellationToken) {
        await ShouldConsumeAfterResubscribe(cancellationToken);
    }
}
