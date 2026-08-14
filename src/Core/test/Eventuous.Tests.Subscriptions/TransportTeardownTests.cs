using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Teardown must run exactly once per run — some transport resources are single-use (a Google Pub/Sub
/// SubscriberClient can't be started twice), and it's the framework's job, not every transport's, to
/// guarantee that.
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
    /// Counts what the framework asks of a transport, without holding any real resources of its own.
    /// </summary>
    sealed class CountingSubscription()
        : EventSubscription<TestOptions>(
            new() { SubscriptionId = "transport-teardown" },
            new ConsumePipe().AddDefaultConsumer(new NoOpHandler()),
            null,
            null
        ) {
        int _stops;

        public int Stops => Volatile.Read(ref _stops);

        protected override ValueTask Connect(SubscriptionRun run) {
            run.OnDisconnect(_ => { Interlocked.Increment(ref _stops); return default; });

            return default;
        }
    }

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }
}
