using EventStore.Client;
using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers.Logging;
using Eventuous.Tests.Subscriptions.Base;
using static Xunit.TestContext;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore.Subscriptions;

[Collection("Database")]
public sealed class StreamSubscriptionDeletedEventsTests : IClassFixture<StoreFixture>, IDisposable {
    readonly StoreFixture         _fixture;
    readonly ILoggerFactory       _loggerFactory;
    readonly LoggingEventListener _listener;

    public StreamSubscriptionDeletedEventsTests(StoreFixture fixture, ITestOutputHelper output) {
        _fixture       = fixture;
        _loggerFactory = LoggerFactory.Create(cfg => cfg.AddXUnit(output).SetMinimumLevel(LogLevel.Debug));
        _listener      = new(_loggerFactory);
        _fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
    }

    [Fact]
    [Trait("Category", "Special cases")]
    public async Task StreamSubscriptionGetsDeletedEvents() {
        var    service        = new BookingService(_fixture.EventStore);
        var    categoryStream = new StreamName("$ce-Booking");
        ulong? startPosition  = null;

        try {
            var last = await _fixture.Client.ReadStreamAsync(Direction.Backwards, categoryStream, StreamPosition.End, 1, cancellationToken: Current.CancellationToken)
                .ToArrayAsync(Current.CancellationToken);
            startPosition = last[0].OriginalEventNumber;
        } catch (StreamNotFoundException) { }

        const int produceCount = 20;
        const int deleteCount  = 5;

        var commands = Enumerable.Range(0, produceCount).Select(_ => DomainFixture.CreateImportBooking()).ToArray();

        foreach (var command in commands) {
            await service.Handle(command, CancellationToken.None);
        }

        var delete = Enumerable.Range(5, deleteCount).Select(x => commands[x]).ToList();

        await Task.WhenAll(
            delete.Select(x => _fixture.EventStore.DeleteStream(StreamName.For<Booking>(x.BookingId), ExpectedStreamVersion.Any, CancellationToken.None))
        );

        var handler = new TestHandler();

        const string subscriptionId = "TestSub";

        var subscription = new StreamSubscription(
            _fixture.Client,
            new() {
                StreamName     = categoryStream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = true,
                ThrowOnError   = true,
            },
            new NoOpCheckpointStore(startPosition),
            new ConsumePipe().AddSystemEventsFilter().AddDefaultConsumer(handler),
            eventSerializer: _fixture.Serializer
        );

        var log = _loggerFactory.CreateLogger("Test");

        await subscription.SubscribeWithLog(log);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));

        while (handler.Count < produceCount - deleteCount && !cts.IsCancellationRequested) {
            await Task.Delay(100, cts.Token);
        }

        await subscription.UnsubscribeWithLog(log);

        var actual = handler.Processed.Select(x => x.Stream.GetId()).ToList();
        actual.Should().BeEquivalentTo(commands.Except(delete).Select(x => x.BookingId));
    }

    class TestHandler : BaseEventHandler {
        public int Count { get; private set; }

        public List<IMessageConsumeContext> Processed { get; } = [];

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext ctx) {
            Count++;

            if (ctx == null) throw new InvalidOperationException();

            Processed.Add(ctx);

            return default;
        }
    }

    public void Dispose() {
        _loggerFactory.Dispose();
        _listener.Dispose();
    }
}
