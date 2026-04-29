using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using KurrentDB.Client;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

public class PersistentSubscriptionFailureTests {
    [Test]
    [Category("Persistent subscription")]
    public async Task Esdb_ShouldParkMessageWhenHandlerFails(CancellationToken cancellationToken) {
        var fixture = new PersistentSubscriptionFixture<StreamPersistentSubscription, StreamPersistentSubscriptionOptions, AlwaysFailingHandler>(
            new(),
            CreateSubscription,
            autoStart: false
        );

        await fixture.InitializeAsync();
        var started = false;

        try {
            await fixture.Start();
            started = true;

            var testEvent = TestEvent.Create();
            await fixture.Producer.Produce(fixture.Stream, testEvent, new(), cancellationToken: cancellationToken);

            var parkedStream = $"$persistentsubscription-{fixture.Stream}::{fixture.SubscriptionId}-parked";
            var parked       = await ReadFirstParkedEvent(fixture.Client, parkedStream, TimeSpan.FromSeconds(20), cancellationToken)
                            ?? throw new TimeoutException($"No event was parked on {parkedStream} within the timeout");

            await Assert.That(parked.Event.EventStreamId).IsEqualTo(fixture.Stream.ToString());
            await Assert.That(parked.Event.EventType).IsEqualTo(TestEvent.TypeName);
            await Assert.That(fixture.Handler.Failures).IsGreaterThan(0);
        } finally {
            // Fixture only auto-stops when autoStart is true, so stop explicitly to avoid leaking the subscription.
            if (started) await fixture.Stop();
            await fixture.DisposeAsync();
        }
    }

    static StreamPersistentSubscription CreateSubscription(string id, string connectionString, StreamName stream, AlwaysFailingHandler handler, ILoggerFactory loggerFactory) {
        var settings = KurrentDBClientSettings.Create(connectionString);

        return new(
            new KurrentDBClient(settings),
            new() {
                StreamName     = stream,
                SubscriptionId = id,
                ThrowOnError   = true,
                SubscriptionSettings = new PersistentSubscriptionSettings(
                    resolveLinkTos: false,
                    messageTimeout: TimeSpan.FromSeconds(2),
                    maxRetryCount: 0
                )
            },
            new ConsumePipe().AddDefaultConsumer(handler),
            loggerFactory
        );
    }

    static async Task<ResolvedEvent?> ReadFirstParkedEvent(KurrentDBClient client, string parkedStream, TimeSpan timeout, CancellationToken cancellationToken) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested) {
            try {
                var read  = client.ReadStreamAsync(
                    Direction.Forwards,
                    parkedStream,
                    StreamPosition.Start,
                    maxCount: 1,
                    resolveLinkTos: true,
                    cancellationToken: cts.Token
                );
                var state = await read.ReadState;

                if (state == ReadState.Ok) {
                    await foreach (var resolved in read) {
                        return resolved;
                    }
                }
            } catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) {
                return null;
            }

            try {
                await Task.Delay(200, cts.Token);
            } catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) {
                return null;
            }
        }

        return null;
    }
}

public class AlwaysFailingHandler : BaseEventHandler {
    int _failures;

    public int Failures => Volatile.Read(ref _failures);

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        Interlocked.Increment(ref _failures);

        throw new InvalidOperationException("Simulated handler failure");
    }
}
