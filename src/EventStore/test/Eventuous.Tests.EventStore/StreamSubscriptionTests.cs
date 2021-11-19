using Eventuous.Diagnostics.Logging;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public sealed class StreamSubscriptionTests : IDisposable {
    readonly ILoggerFactory       _loggerFactory;
    readonly LoggingEventListener _listener;

    public StreamSubscriptionTests(ITestOutputHelper output) {
        _loggerFactory = LoggerFactory.Create(
            cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Debug)
        );

        _listener = new LoggingEventListener(_loggerFactory);
    }

    [Fact]
    public async Task StreamSubscriptionGetsDeletedEvents() {
        var service = new BookingService(Instance.AggregateStore);

        var categoryStream = new StreamName("$ce-Booking");

        ulong? startPosition = null;

        try {
            var last = await Instance.EventStore.ReadEventsBackwards(
                categoryStream,
                1,
                CancellationToken.None
            );

            startPosition = (ulong?)last[0].Position;
        }
        catch (StreamNotFound) { }

        const int produceCount = 20;
        const int deleteCount  = 5;

        var commands = Enumerable.Range(0, produceCount)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToArray();

        await Task.WhenAll(
            commands.Select(x => service.Handle(x, CancellationToken.None))
        );

        var delete = Enumerable.Range(5, deleteCount).Select(x => commands[x]).ToList();

        await Task.WhenAll(
            delete
                .Select(
                    x => Instance.EventStore.DeleteStream(
                        StreamName.For<Booking>(x.BookingId),
                        ExpectedStreamVersion.Any,
                        CancellationToken.None
                    )
                )
        );

        var handler = new TestHandler();

        const string subscriptionId = "TestSub";

        var subscription = new StreamSubscription(
            Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = categoryStream,
                SubscriptionId = subscriptionId,
                ResolveLinkTos = true,
                ThrowOnError   = true
            },
            new NoOpCheckpointStore(startPosition),
            new ConsumePipe().AddDefaultConsumer(handler)
        );

        var log = _loggerFactory.CreateLogger("Test");

        await subscription.SubscribeWithLog(log);

        while (handler.Count < produceCount - deleteCount) {
            await Task.Delay(100);
        }

        await subscription.UnsubscribeWithLog(log);

        log.LogInformation("Received {Count} events", handler.Count);

        var actual = handler.Processed
            .Select(x => (x.Message as BookingEvents.BookingImported)!.BookingId)
            .ToList();

        log.LogInformation("Actual contains {Count} events", actual.Count);

        actual
            .Should()
            .BeEquivalentTo(commands.Except(delete).Select(x => x.BookingId));
    }

    class TestHandler : BaseEventHandler {
        public int Count { get; private set; }

        public List<IMessageConsumeContext> Processed { get; } = new();

        public override ValueTask<EventHandlingStatus> HandleEvent(
            IMessageConsumeContext ctx
        ) {
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