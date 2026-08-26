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

[ClassDataSource<StreamNameFixture>(Shared = SharedType.None)]
[NotInParallel]
public class StreamSubscriptionMeasure(StreamNameFixture streamNameFixture)
    : SubscriptionMeasureBase<MsSqlContainer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, SqlServerCheckpointStore>(
        new SubscriptionFixture<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, TestEventHandler>(
            opt => opt.Stream = streamNameFixture.StreamName,
            false
        )
    ) {
    [Test]
    public async Task SqlServer_ShouldMeasureEndOfSubscribedStream(CancellationToken cancellationToken) {
        await ShouldMeasureEndOfSubscribedStream(streamNameFixture.StreamName, cancellationToken);
    }
}
