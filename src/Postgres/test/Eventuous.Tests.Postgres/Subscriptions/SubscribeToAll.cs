using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Postgres.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscribeToAll(ITestOutputHelper outputHelper)
    : SubscribeToAllBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(outputHelper) {
    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();

    protected override PostgresCheckpointStore GetCheckpointStore(IServiceProvider sp) => throw new NotImplementedException();

    protected override void ConfigureSubscription(PostgresAllStreamSubscriptionOptions options) => throw new NotImplementedException();
}
