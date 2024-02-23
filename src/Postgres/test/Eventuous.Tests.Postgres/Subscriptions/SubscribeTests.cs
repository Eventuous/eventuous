using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.PostgreSql;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Subscriptions;

[Collection("Database")]
public class SubscribeToAll(ITestOutputHelper outputHelper)
    : SubscribeToAllBase<PostgreSqlContainer, PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, PostgresCheckpointStore>(
        outputHelper,
        new SubscriptionFixture<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, TestEventHandler>(_ => { }, outputHelper, false)
    ) {
    [Fact]
    public async Task Postgres_ShouldConsumeProducedEvents() {
        await ShouldConsumeProducedEvents();
    }

    [Fact]
    public async Task Postgres_ShouldConsumeProducedEventsWhenRestarting() {
        await ShouldConsumeProducedEventsWhenRestarting();
    }

    [Fact]
    public async Task Postgres_ShouldUseExistingCheckpoint() {
        await ShouldUseExistingCheckpoint();
    }
}

[Collection("Database")]
public class SubscribeToStream(ITestOutputHelper outputHelper, StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<PostgreSqlContainer, PostgresStreamSubscription, PostgresStreamSubscriptionOptions, PostgresCheckpointStore>(
            outputHelper,
            streamNameFixture.StreamName,
            new SubscriptionFixture<PostgresStreamSubscription, PostgresStreamSubscriptionOptions, TestEventHandler>(
                opt => ConfigureOptions(opt, streamNameFixture),
                outputHelper,
                false
            )
        ),
        IClassFixture<StreamNameFixture> {

    [Fact]
    public async Task Postgres_ShouldConsumeProducedEvents() {
        await ShouldConsumeProducedEvents();
    }

    [Fact]
    public async Task Postgres_ShouldConsumeProducedEventsWhenRestarting() {
        await ShouldConsumeProducedEventsWhenRestarting();
    }

    [Fact]
    public async Task Postgres_ShouldUseExistingCheckpoint() {
        await ShouldUseExistingCheckpoint();
    }


    static void ConfigureOptions(PostgresStreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.Stream = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    static readonly Fixture Auto = new();

    public StreamName StreamName = new(Auto.Create<string>());
}
