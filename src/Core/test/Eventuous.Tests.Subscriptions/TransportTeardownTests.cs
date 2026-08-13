using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A transport's Unsubscribe releases whatever its Subscribe built, and some of what it releases is
/// single-use: a Google Pub/Sub SubscriberClient cannot be started twice, and an Azure Service Bus
/// processor is replaced rather than reused. So teardown has to run once per run, and it is the
/// framework's job to make sure of it — otherwise every transport has to be defensively idempotent.
/// </summary>
public class TransportTeardownTests {
    [Test]
    public async Task Teardown_runs_once_when_unsubscribe_is_called_twice(CancellationToken ct) {
        var subscription = new CountingSubscription();

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);
        await subscription.Unsubscribe(_ => { }, ct);
        await subscription.Unsubscribe(_ => { }, ct);

        subscription.Stops.ShouldBe(1, "the transport was torn down again with nothing left to tear down");
    }

    [Test]
    public async Task Teardown_does_not_run_when_the_subscription_never_started(CancellationToken ct) {
        var subscription = new CountingSubscription();

        await subscription.Unsubscribe(_ => { }, ct);

        subscription.Stops.ShouldBe(0, "a transport that was never subscribed has nothing to release");
    }

    record TestOptions : SubscriptionOptions;

    /// <summary>
    /// Counts what the framework asks of a transport. It holds no resources, because the point is the
    /// call pattern rather than what a real transport would do with it.
    /// </summary>
    sealed class CountingSubscription()
        : EventSubscription<TestOptions>(
            new() { SubscriptionId = "transport-teardown" },
            new ConsumePipe().AddDefaultConsumer(new NoOpHandler()),
            null,
            null
        ) {
        int _starts;
        int _stops;

        public int Starts => Volatile.Read(ref _starts);
        public int Stops  => Volatile.Read(ref _stops);

        protected override ValueTask Subscribe(CancellationToken cancellationToken) {
            Interlocked.Increment(ref _starts);

            return default;
        }

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) {
            Interlocked.Increment(ref _stops);

            return default;
        }
    }

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }
}
