using Eventuous.Subscriptions;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Logging;
using Eventuous.Sut.App;
using Eventuous.Sut.Domain;
using Eventuous.Tests.EventStore.Fixtures;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using static Eventuous.Tests.EventStore.Fixtures.IntegrationFixture;
using StreamSubscription = Eventuous.EventStore.Subscriptions.StreamSubscription;

namespace Eventuous.Tests.EventStore;

public class StreamSubscriptionTests {
    readonly ILoggerFactory _loggerFactory;

    public StreamSubscriptionTests(ITestOutputHelper output) {
        _loggerFactory = LoggerFactory.Create(
            cfg => cfg.AddXunit(output).SetMinimumLevel(LogLevel.Debug)
        );
    }

    [Fact]
    public async Task StreamSubscriptionGetsDeletedEvents() {
        var service = new BookingService(Instance.AggregateStore);

        const string categoryStream = "$ce-Booking";

        ulong? startPosition = null;

        try {
            var last = await Instance.EventStore.ReadEventsBackwards(
                new StreamName(categoryStream),
                1,
                CancellationToken.None
            );

            startPosition = (ulong?)last[0].Position;
        }
        catch (Exceptions.StreamNotFound) { }

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

        var subscription = new StreamSubscription(
            Instance.Client,
            new StreamSubscriptionOptions {
                StreamName     = categoryStream,
                SubscriptionId = "TestSub",
                ResolveLinkTos = true,
                ThrowOnError   = true
            },
            new NoOpCheckpointStore(startPosition),
            new[] { handler },
            _loggerFactory
        );

        await subscription.StartAsync(CancellationToken.None);

        while (handler.Count < 90) {
            await Task.Delay(100);
        }

        await subscription.StopAsync(CancellationToken.None);

        handler.Processed
            .Select(x => (x as BookingEvents.BookingImported)!.BookingId)
            .Should()
            .BeEquivalentTo(commands.Except(delete).Select(x => x.BookingId));
    }

    class TestHandler : IEventHandler {
        public ulong Position { get; private set; }
        public int   Count    { get; private set; }

        public List<object> Processed { get; } = new();

        public void SetLogger(SubscriptionLog subscriptionLogger) { }

        public Task HandleEvent(
            ReceivedEvent     evt,
            CancellationToken cancellationToken
        ) {
            Position = evt.StreamPosition;
            Count++;
            if (evt == null) throw new InvalidOperationException();

            Processed.Add(evt);

            return Task.CompletedTask;
        }
    }
}