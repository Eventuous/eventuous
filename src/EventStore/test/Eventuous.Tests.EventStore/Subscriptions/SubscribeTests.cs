using Eventuous.EventStore.Subscriptions;
using Eventuous.Tests.EventStore.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.EventStoreDb;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Subscriptions;

public class SubscribeToAll()
    : SubscribeToAllBase<EventStoreDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore>(
        new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(_ => { }, new("$all"), false)
    ) {
    [Test]
    public async Task Esdb_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task Esdb_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task Esdb_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }
}

[ClassDataSource<StreamNameFixture>(Shared = SharedType.None)]
public class SubscribeToStream(StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<EventStoreDbContainer, StreamSubscription, StreamSubscriptionOptions, TestCheckpointStore>(
        streamNameFixture.StreamName,
        new CatchUpSubscriptionFixture<StreamSubscription, StreamSubscriptionOptions, TestEventHandler>(
            opt => ConfigureOptions(opt, streamNameFixture),
            streamNameFixture.StreamName,
            false
        )
    ) {
    [Before(Test)]
    public async Task Setup() => await InitializeAsync();

    [After(Test)]
    public async Task TearDown() => await DisposeAsync();
    
    [Test]
    public async Task Esdb_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    public async Task Esdb_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    public async Task Esdb_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }

    static void ConfigureOptions(StreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.StreamName = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    public StreamName StreamName = new(Guid.NewGuid().ToString("N"));
}
