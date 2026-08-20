using Eventuous.Redis.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Shouldly;
using StackExchange.Redis;

namespace Eventuous.Tests.Redis.Subscriptions;

/// <summary>
/// The polling loop is the whole subscription, so these tests fail at the ReadEvents seam and need no server.
/// </summary>
public class PollFailureTests {
    /// <summary>
    /// StackExchange.Redis reconnects underneath us, so a dropped connection should cost at most a poll.
    /// That only helps if something polls again.
    /// </summary>
    [Test]
    public async Task Poll_failure_is_followed_by_another_poll(CancellationToken ct) {
        var subscription = new FailingSubscription(failuresBeforeSuccess: 1);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var polledAgain = await WaitUntil(() => subscription.Polls > 1, TimeSpan.FromSeconds(10));

        await subscription.Unsubscribe(_ => { }, ct);

        polledAgain.ShouldBeTrue($"the subscription should keep polling after a failure, it polled {subscription.Polls} time(s)");
    }

    /// <summary>
    /// A subscription that has stopped consuming has to say so, or the host reports it healthy forever.
    /// </summary>
    [Test]
    public async Task Poll_failure_is_reported_as_a_drop(CancellationToken ct) {
        var subscription = new FailingSubscription(failuresBeforeSuccess: 1);

        DropReason? reason = null;
        await subscription.Subscribe(_ => { }, (_, r, _) => reason = r, ct);

        var reported = await WaitUntil(() => reason != null, TimeSpan.FromSeconds(10));

        await subscription.Unsubscribe(_ => { }, ct);

        reported.ShouldBeTrue("a failed poll should be reported through the dropped callback, so health checks see it");
    }

    static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (condition()) return true;

            await Task.Delay(20);
        }

        return condition();
    }

    record TestOptions : RedisSubscriptionBaseOptions;

    /// <summary>
    /// Fails its first <c>failuresBeforeSuccess</c> polls the way the driver would, then returns nothing.
    /// The database is never touched, so no server is involved.
    /// </summary>
    sealed class FailingSubscription(int failuresBeforeSuccess)
        : RedisSubscriptionBase<TestOptions>(
            () => null!,
            new() { SubscriptionId = "redis-poll-failure", MaxPageSize = 10 },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new NoOpHandler()),
            SubscriptionKind.All,
            null
        ) {
        int _polls;

        public int Polls => Volatile.Read(ref _polls);

        protected override Task<ReceivedEvent[]> ReadEvents(IDatabase database, long position) {
            var poll = Interlocked.Increment(ref _polls);

            if (poll <= failuresBeforeSuccess) throw new RedisConnectionException(ConnectionFailureType.SocketFailure, "Simulated connection failure");

            return Task.FromResult(Array.Empty<ReceivedEvent>());
        }
    }

    sealed class NoOpHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }
}
