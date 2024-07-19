using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.EventStoreDb;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public class SubscribeToAll : SubscribeToAllBase<EventStoreDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore> {
    public SubscribeToAll(ITestOutputHelper outputHelper)
        : base(
            outputHelper,
            new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(_ => { }, outputHelper, new("$all"), false)
        ) {
    }

    [Fact]
    public async Task Esdb_ShouldConsumeProducedEvents() {
        await ShouldConsumeProducedEvents();
    }

    [Fact]
    public async Task Esdb_ShouldConsumeProducedEventsWhenRestarting() {
        await ShouldConsumeProducedEventsWhenRestarting();
    }

    [Fact]
    public async Task Esdb_ShouldUseExistingCheckpoint() {
        await ShouldUseExistingCheckpoint();
    }
}

[Collection("Database")]
public class SubscribeToStream(ITestOutputHelper outputHelper, StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<EventStoreDbContainer, StreamSubscription, StreamSubscriptionOptions, TestCheckpointStore>(
            outputHelper,
            streamNameFixture.StreamName,
            new CatchUpSubscriptionFixture<StreamSubscription, StreamSubscriptionOptions, TestEventHandler>(
                opt => ConfigureOptions(opt, streamNameFixture),
                outputHelper,
                streamNameFixture.StreamName,
                false
            )
        ),
        IClassFixture<StreamNameFixture> {
    [Fact]
    public async Task Esdb_ShouldConsumeProducedEvents() {
        await ShouldConsumeProducedEvents();
    }

    [Fact]
    public async Task Esdb_ShouldConsumeProducedEventsWhenRestarting() {
        await ShouldConsumeProducedEventsWhenRestarting();
    }

    [Fact]
    public async Task Esdb_ShouldUseExistingCheckpoint() {
        await ShouldUseExistingCheckpoint();
    }

    static void ConfigureOptions(StreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.StreamName = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    static readonly Fixture Auto = new();

    public StreamName StreamName = new(Auto.Create<string>());
}
