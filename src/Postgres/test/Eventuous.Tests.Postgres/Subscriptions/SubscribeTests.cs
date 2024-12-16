using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscribeToAll()
    : SubscribeToAllBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        new SubscriptionFixture<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(_ => { }, false)
    ) {
    [Test]
    public async Task Postgres_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task Postgres_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task Postgres_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }
}

[ClassDataSource<StreamNameFixture>]
public class SubscribeToStream(StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<PostgreSqlContainer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions, PostgresCheckpointStore>(
        streamNameFixture.StreamName,
        new SubscriptionFixture<PostgresStreamSubscription, PostgresStreamSubscriptionOptions, TestEventHandler>(
            opt => ConfigureOptions(opt, streamNameFixture),
            false
        )
    ) {
    [Test]
    public async Task Postgres_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task Postgres_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task Postgres_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }

    static void ConfigureOptions(PostgresStreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.Stream = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    public StreamName StreamName = new(Guid.NewGuid().ToString());
}
