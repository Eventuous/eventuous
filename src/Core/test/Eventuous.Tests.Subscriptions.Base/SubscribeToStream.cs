using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscribeToStreamBase<TContainer, TSub, TSubOptions, TCheckpointStore>(
        StreamName                                                                                 streamName,
        SubscriptionFixtureBase<TContainer, TSub, TSubOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSub : EventSubscription<TSubOptions>
    where TSubOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    protected async Task ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        const int   count    = 10;
        const ulong expected = count - 1;

        var testEvents = await GenerateAndProduceEvents(count);

        await fixture.StartSubscription();
        await fixture.Handler.AssertCollection(TimeSpan.FromSeconds(2), [..testEvents]).Validate(cancellationToken);
        await fixture.StopSubscription();
        await Assert.That(fixture.Handler.Count).IsEqualTo(10);

        var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        await Assert.That(checkpoint.Position).IsEqualTo(expected);
    }

    protected async Task ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        TestContext.Current?.OutputWriter.WriteLine("Phase one");
        await TestConsumptionOfProducedEvents();

        TestContext.Current?.OutputWriter.WriteLine("Resetting handler");
        fixture.Handler.Reset();

        TestContext.Current?.OutputWriter.WriteLine("Phase two");
        await TestConsumptionOfProducedEvents();

        var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        await Assert.That(checkpoint.Position).IsEqualTo(19UL);

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            TestContext.Current?.OutputWriter.WriteLine("Generating and producing events");
            var testEvents = await GenerateAndProduceEvents(count);

            TestContext.Current?.OutputWriter.WriteLine("Starting subscription");
            await fixture.StartSubscription();
            await fixture.Handler.AssertCollection(TimeSpan.FromSeconds(2), [..testEvents]).Validate();
            TestContext.Current?.OutputWriter.WriteLine("Stopping subscription");
            await fixture.StopSubscription();
            await Assert.That(fixture.Handler.Count).IsEqualTo(10);
        }
    }

    public async Task ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        const int count = 10;

        await GenerateAndProduceEvents(count);

        await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        Logger.ConfigureIfNull(fixture.SubscriptionId, fixture.LoggerFactory);
        await fixture.CheckpointStore.StoreCheckpoint(new(fixture.SubscriptionId, 9), true, cancellationToken);

        await fixture.StartSubscription();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await fixture.StopSubscription();
        await Assert.That(fixture.Handler.Count).IsEqualTo(0);
    }

    static BookingImported ToEvent(ImportBooking cmd)
        => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        await TestContext.Current!.OutputWriter.WriteLineAsync($"Producing events to {streamName}")!;

        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking(fixture.Auto))
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));
        await fixture.EventStore.AppendEvents(streamName, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }

    protected async Task InitializeAsync() => await fixture.InitializeAsync();

    protected async Task DisposeAsync() => await fixture.DisposeAsync();
}
