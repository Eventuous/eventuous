using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Subscriptions;

[NotInParallel]
public class SubscriptionRestart()
    : SubscriptionRestartBase<MsSqlContainer, SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, SqlServerCheckpointStore>(
        new SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task SqlServer_ShouldTolerateRepeatedUnsubscribe() {
        await ShouldTolerateRepeatedUnsubscribe();
    }

    [Test]
    public async Task SqlServer_ShouldConsumeAfterResubscribe(CancellationToken cancellationToken) {
        await ShouldConsumeAfterResubscribe(cancellationToken);
    }
}
