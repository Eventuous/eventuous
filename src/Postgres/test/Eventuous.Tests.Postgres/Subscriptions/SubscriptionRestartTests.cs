using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class SubscriptionRestart()
    : SubscriptionRestartBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<PostgresStore, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task Postgres_ShouldTolerateRepeatedUnsubscribe() {
        await ShouldTolerateRepeatedUnsubscribe();
    }

    [Test]
    public async Task Postgres_ShouldConsumeAfterResubscribe(CancellationToken cancellationToken) {
        await ShouldConsumeAfterResubscribe(cancellationToken);
    }
}
