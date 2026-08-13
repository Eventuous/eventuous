using Eventuous.GooglePubSub.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;

namespace Eventuous.Tests.GooglePubSub;

/// <summary>
/// A SubscriberClient cannot be started twice — the SDK documents StartAsync as callable once per
/// instance — so the subscription builds a new one on every Subscribe. That only holds together if
/// teardown releases the one it stopped and never touches it again. No emulator is involved: the
/// constructor only resolves resource names, so teardown can be exercised on its own.
///
/// Both tests assert by completing. An exception out of teardown fails them.
/// </summary>
public class TeardownTests {
    [Test]
    public async Task Teardown_without_a_subscribe_completes(CancellationToken ct) {
        var subscription = Create();

        // Reachable in production, not only from a test: Subscribe records the run before awaiting the
        // transport, so a Subscribe that throws part-way leaves shutdown tearing down a run that never
        // got as far as its subscriber task.
        await subscription.Unsubscribe(_ => { }, ct);
    }

    [Test]
    public async Task Teardown_twice_completes(CancellationToken ct) {
        var subscription = Create();

        await subscription.Unsubscribe(_ => { }, ct);
        await subscription.Unsubscribe(_ => { }, ct);
    }

    static GooglePubSubSubscription Create()
        => new("test-project", "test-topic", "test-subscription", new ConsumePipe().AddDefaultConsumer(new NoOpHandler()));

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }
}
