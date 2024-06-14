using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscribeToStreamBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        ITestOutputHelper                                                                                            outputHelper,
        StreamName                                                                                                   streamName,
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : IAsyncLifetime
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    protected async Task ShouldConsumeProducedEvents() {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);

        await fixture.StartSubscription();
        await fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate();
        await fixture.StopSubscription();
        fixture.Handler.Count.Should().Be(10);

        var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, default);
        checkpoint.Position.Should().Be(count - 1);
    }

    protected async Task ShouldConsumeProducedEventsWhenRestarting() {
        outputHelper.WriteLine("Phase one");
        await TestConsumptionOfProducedEvents();

        outputHelper.WriteLine("Resetting handler");
        fixture.Handler.Reset();
        // await InitializeAsync();

        outputHelper.WriteLine("Phase two");
        await TestConsumptionOfProducedEvents();

        var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, default);
        checkpoint.Position.Should().Be(19);

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            outputHelper.WriteLine("Generating and producing events");
            var testEvents = await GenerateAndProduceEvents(count);

            outputHelper.WriteLine("Starting subscription");
            await fixture.StartSubscription();
            await fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate();
            outputHelper.WriteLine("Stopping subscription");
            await fixture.StopSubscription();
            fixture.Handler.Count.Should().Be(10);
        }
    }

    public async Task ShouldUseExistingCheckpoint() {
        const int count = 10;

        await GenerateAndProduceEvents(count);

        await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, default);
        Logger.ConfigureIfNull(fixture.SubscriptionId, fixture.LoggerFactory);
        await fixture.CheckpointStore.StoreCheckpoint(new Checkpoint(fixture.SubscriptionId, 9), true, default);

        await fixture.StartSubscription();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await fixture.StopSubscription();
        fixture.Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        outputHelper.WriteLine($"Producing events to {streamName}");
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(fixture.Auto))
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new StreamEvent(Guid.NewGuid(), x, new Metadata(), "", 0));
        await fixture.EventStore.AppendEvents(streamName, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }

    public Task InitializeAsync() => fixture.InitializeAsync();

    public Task DisposeAsync() => fixture.DisposeAsync();
}
