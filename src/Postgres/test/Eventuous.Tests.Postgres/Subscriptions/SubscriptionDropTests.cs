using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class SubscriptionDrop()
    : SubscriptionDropBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<PostgresStore, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(
            _ => { },
            false
        )
    ) {
    [Test]
    public async Task Postgres_ShouldResubscribeAfterConnectionDrop(CancellationToken cancellationToken) {
        await ShouldResubscribeAfterConnectionDrop(cancellationToken);
    }
}
