using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Subscriptions;

[NotInParallel]
public class SubscriptionMeasure()
    : SubscriptionMeasureBase<MsSqlContainer, SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, SqlServerCheckpointStore>(
        new SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task SqlServer_ShouldMeasureEndOfStream(CancellationToken cancellationToken) {
        await ShouldMeasureEndOfStream(cancellationToken);
    }
}
