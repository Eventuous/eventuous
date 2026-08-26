using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class SubscriptionMeasure()
    : SubscriptionMeasureBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<PostgresStore, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task Postgres_ShouldMeasureEndOfStream(CancellationToken cancellationToken) {
        await ShouldMeasureEndOfStream(cancellationToken);
    }
}

[ClassDataSource<StreamNameFixture>(Shared = SharedType.None)]
[NotInParallel]
public class StreamSubscriptionMeasure(StreamNameFixture streamNameFixture)
    : SubscriptionMeasureBase<PostgreSqlContainer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<PostgresStore, PostgresStreamSubscription, PostgresStreamSubscriptionOptions, TestEventHandler>(
            opt => opt.Stream = streamNameFixture.StreamName,
            false
        )
    ) {
    [Test]
    public async Task Postgres_ShouldMeasureEndOfSubscribedStream(CancellationToken cancellationToken) {
        await ShouldMeasureEndOfSubscribedStream(streamNameFixture.StreamName, cancellationToken);
    }
}
