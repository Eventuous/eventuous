using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Sut.Subs;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public class StreamSubscriptionTests {
    readonly ILoggerFactory _loggerFactory;

    public StreamSubscriptionTests(ITestOutputHelper output) {
        _loggerFactory = LoggerFactory.Create(
            cfg => cfg.AddXunit(output, LogLevel.Debug).SetMinimumLevel(LogLevel.Debug)
        );
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

        var commands = Enumerable.Range(0, 100)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToArray();

        await Task.WhenAll(
            commands.Select(x => service.Handle(x, CancellationToken.None))
        );

        var delete = Enumerable.Range(20, 10).Select(x => commands[x]).ToList();

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
            new DefaultConsumer(new IEventHandler[] { handler }),
            _loggerFactory
        );

        var log = _loggerFactory.CreateLogger("Test");

        await subscription.SubscribeWithLog(log);

        while (handler.Count < 90) {
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

    class TestHandler : IEventHandler {
        public int Count { get; private set; }

        public List<IMessageConsumeContext> Processed { get; } = new();

        public ValueTask HandleEvent(
            IMessageConsumeContext evt,
            CancellationToken      cancellationToken
        ) {
            Count++;
            if (evt == null) throw new InvalidOperationException();

            Processed.Add(evt);

            return default;
        }
    }
}