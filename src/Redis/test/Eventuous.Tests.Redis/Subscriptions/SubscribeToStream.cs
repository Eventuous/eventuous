using Eventuous.Subscriptions.Logging;
using Eventuous.Tests.Redis.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Redis.Subscriptions;

public class SubscribeToStream {
    SubscriptionFixture<TestEventHandler> _fixture = null!;

    [Before(Test)]
    public async Task Setup() {
        _fixture = new(false);
        await _fixture.InitializeAsync();
    }

    [After(Test)]
    public async Task TearDown() {
        await _fixture.DisposeAsync();
    }

    [Test]
    [Retry(5)]
    public async Task ShouldConsumeProducedEvents(CancellationToken cancellationToken) {
        const int count = 10;

        var testEvents = await GenerateAndProduceEvents(count);

        await _fixture.Start();
        await _fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate(cancellationToken);
        await _fixture.Stop();

        _fixture.Handler.Count.Should().Be(10);
    }

    [Test]
    [Retry(5)]
    public async Task ShouldConsumeProducedEventsWhenRestarting(CancellationToken cancellationToken) {
        await TestConsumptionOfProducedEvents();

        _fixture.Handler.Reset();
        await _fixture.InitializeAsync();

        await TestConsumptionOfProducedEvents();

        return;

        async Task TestConsumptionOfProducedEvents() {
            const int count = 10;

            var testEvents = await GenerateAndProduceEvents(count);

            await _fixture.Start();
            await _fixture.Handler.AssertCollection(2.Seconds(), [..testEvents]).Validate(cancellationToken);
            await _fixture.Stop();

            _fixture.Handler.Count.Should().Be(10);
        }
    }

    [Test]
    [Retry(5)]
    public async Task ShouldUseExistingCheckpoint(CancellationToken cancellationToken) {
        const int count = 10;

        await GenerateAndProduceEvents(count);

        await _fixture.CheckpointStore.GetLastCheckpoint(_fixture.SubscriptionId, cancellationToken);
        var streamPosition = await GetStreamPosition(count);
        Logger.ConfigureIfNull(_fixture.SubscriptionId, _fixture.LoggerFactory);
        await _fixture.CheckpointStore.StoreCheckpoint(new(_fixture.SubscriptionId, (ulong)streamPosition), true, cancellationToken);

        await _fixture.Start();
        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        await _fixture.Stop();
        _fixture.Handler.Count.Should().Be(0);
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    async Task<List<BookingImported>> GenerateAndProduceEvents(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var events       = commands.Select(ToEvent).ToList();
        var streamEvents = events.Select(x => new NewStreamEvent(Guid.NewGuid(), x, new()));

        await _fixture.IntegrationFixture.EventWriter.AppendEvents(_fixture.Stream, ExpectedStreamVersion.Any, streamEvents.ToArray(), default);

        return events;
    }

    async Task<long> GetStreamPosition(int count) {
        var readEvents = await _fixture.IntegrationFixture.EventReader.ReadEvents(_fixture.Stream, StreamReadPosition.Start, count, true, default);

        return readEvents.Last().Position;
    }
}
