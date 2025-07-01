using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.Subscriptions.Base;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore.Subscriptions;

public sealed class StreamSubscriptionDeletedEventsTests {
    StoreFixture         _fixture = null!;
    ILoggerFactory       _loggerFactory = null!;
    LoggingEventListener _listener = null!;

    [Test]
    [Category("Special cases")]
    public async Task StreamSubscriptionGetsDeletedEvents(CancellationToken cancellationToken) {
        var    service        = new BookingService(_fixture.EventStore);
        var    categoryStream = new StreamName("$ce-Booking");
        ulong? startPosition  = null;

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

        var expected = commands.Except(delete).Select(x => x.BookingId);
        var log      = _loggerFactory.CreateLogger("Test");

        LogCollection("Produced", commands);
        LogCollection("Deleted", delete);
        LogCollection("Expected", commands.Except(delete));

        await subscription.SubscribeWithLog(log, cancellationToken);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(200));

        while (handler.Count < produceCount - deleteCount && !cts.IsCancellationRequested) {
            await Task.Delay(100, cts.Token);
        }

        await subscription.UnsubscribeWithLog(log, cancellationToken);

        var actual = handler.Processed.Select(x => x.Stream.GetId()).ToList();
        log.LogInformation("Actual:\n {Join}", string.Join("\n", actual));
        await Assert.That(actual).IsEquivalentTo(expected);

        return;

        void LogCollection(string what, IEnumerable<Commands.ImportBooking> collection) => log.LogInformation("{What}:\n {Join}", what, string.Join("\n", collection.Select(x => x.BookingId)));
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

    [Before(Test)]
    public Task Setup() {
        _fixture       = new(LogLevel.Information);
        _loggerFactory = LoggingExtensions.GetLoggerFactory();
        _listener      = new(_loggerFactory);
        _fixture.TypeMapper.RegisterKnownEventTypes(typeof(BookingEvents.BookingImported).Assembly);
        return _fixture.InitializeAsync();
    }

    [After(Test)]
    public async ValueTask Cleanup() {
        await _fixture.DisposeAsync();
        _loggerFactory.Dispose();
        _listener.Dispose();
    }
}
