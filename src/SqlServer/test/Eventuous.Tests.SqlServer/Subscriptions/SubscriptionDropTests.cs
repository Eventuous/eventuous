using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Subscriptions;

[NotInParallel]
public class SubscriptionDrop()
    : SubscriptionDropBase<MsSqlContainer, SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, SqlServerCheckpointStore>(
        new SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task SqlServer_ShouldResubscribeAfterConnectionDrop(CancellationToken cancellationToken) {
        await ShouldResubscribeAfterConnectionDrop(cancellationToken);
    }
}
