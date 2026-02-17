using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.KurrentDb;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.KurrentDB.Subscriptions;

    public class SubscribeToAllFromEnd()
    : SubscribeToAllBase<KurrentDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore>(
        new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(opt => opt.StartFrom = InitialPosition.Latest, new("$all"), false)
    ) {
    [Test]
    [Retry(3)]
    public async Task Esdb_ShouldStartConsumptionFromEnd(CancellationToken cancellationToken) {
        await ShouldStartConsumptionFromEnd(cancellationToken);
    }
}

    public class SubscribeToAll()
    : SubscribeToAllBase<KurrentDbContainer, AllStreamSubscription, AllStreamSubscriptionOptions, TestCheckpointStore>(
        new CatchUpSubscriptionFixture<AllStreamSubscription, AllStreamSubscriptionOptions, TestEventHandler>(_ => { }, new("$all"), false)
    ) {
    [Test]
    [Retry(3)]
    public async Task Esdb_ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEvents(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task Esdb_ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await ShouldConsumeProducedEventsWhenRestarting(cancellationToken);
    }

    [Test]
    [Retry(3)]
    public async Task Esdb_ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        await ShouldUseExistingCheckpoint(cancellationToken);
    }
}

[ClassDataSource<StreamNameFixture>(Shared = SharedType.None)]
public class SubscribeToStream(StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<KurrentDbContainer, StreamSubscription, StreamSubscriptionOptions, TestCheckpointStore>(
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
