using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A failure can be reported from any thread, at any time, including on a run shutdown has already ended.
/// That used to cost a spurious warning, an unobserved exception, or a commit-handler dispose racing the
/// one in teardown (AI-1699); now it should cost nothing at all.
/// </summary>
public class SubscriptionShutdownTests {
    /// <summary>
    /// A failure arriving after the subscription's lifetime is cancelled has nothing to restart, observed
    /// by counting connects — a second one would mean the failure bought an unwanted replacement run.
    /// </summary>
    [Test]
    public async Task Failure_after_shutdown_started_does_not_reconnect(CancellationToken ct) {
        var subscription = new TestSubscription(new() { SubscriptionId = "test-fail-when-cancelled", RetryDelay = ShortRetry }, new ConsumePipe(), new CapturingLoggerFactory());

        using var host = CancellationTokenSource.CreateLinkedTokenSource(ct);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, host.Token);

        // Cancelled through the token Subscribe was given — the route shutdown uses — not the subscription's own.
        await host.CancelAsync();
        subscription.Fail();

        (await Wait.Until(() => !subscription.IsRunning, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the cancelled subscription should have finished stopping");

        // Ten retry delays after it stopped: an unwanted replacement run would have connected by now.
        await Task.Delay(TimeSpan.FromMilliseconds(200), ct);

        subscription.Connects.ShouldBe(1, "a subscription that is already stopping has nothing to reconnect to");
        subscription.IsRunning.ShouldBeFalse();
    }

    /// <summary>
    /// The losing side of the race: shutdown disposes the run's source while a failure is still being
    /// reported against it. Run many times, since the window can't be reconstructed deterministically.
    /// </summary>
    [Test]
    public async Task Failure_racing_unsubscribe_does_not_report_a_disposed_cts(CancellationToken ct) {
        var logs = new CapturingLoggerFactory();

        for (var i = 0; i < 50; i++) {
            var subscription = new TestSubscription(new() { SubscriptionId = $"test-fail-race-{i}", RetryDelay = ShortRetry }, new ConsumePipe(), logs);

            await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

            var failing = Task.Run(
                () => {
                    for (var fail = 0; fail < 20; fail++) subscription.Fail();
                },
                ct
            );

            await subscription.Unsubscribe(_ => { }, ct);
            await failing;
        }

        // Matches text that only appears in an ObjectDisposedException's message, not just the log template.
        var reported = logs.Contains("CancellationTokenSource has been disposed");

        reported.ShouldBeFalse("failing a run while shutdown tears it down is a benign race, not an error");
    }

    /// <summary>
    /// Two shutdown paths racing: whichever caller reads the session before the supervisor retires it
    /// performs the stop, but <c>OnUnsubscribed</c> answers "is this subscription stopped", not "did this
    /// call stop it" — so both callers are owed an answer, and neither may hang or throw.
    /// </summary>
    /// <remarks>
    /// Does not reach the window <c>Unsubscribe</c>'s <c>ObjectDisposedException</c> clause guards — 150
    /// attempts never landed it. That clause stands on AI-1699, not on this test.
    /// </remarks>
    [Test]
    public async Task Concurrent_unsubscribes_do_not_report_a_disposed_cts(CancellationToken ct) {
        var logs = new CapturingLoggerFactory();

        for (var i = 0; i < 50; i++) {
            var subscription = new TestSubscription(new() { SubscriptionId = $"test-stop-race-{i}", RetryDelay = ShortRetry }, new ConsumePipe(), logs);

            await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

            var stops = 0;
            var second = Task.Run(async () => await subscription.Unsubscribe(_ => Interlocked.Increment(ref stops), ct), ct);

            await subscription.Unsubscribe(_ => Interlocked.Increment(ref stops), ct);
            await second;

            stops.ShouldBe(2, "both callers asked to be told the subscription stopped, and for both of them it is");
            subscription.IsRunning.ShouldBeFalse("both callers returned, so the subscription is down either way");
        }

        var reported = logs.Contains("CancellationTokenSource has been disposed");

        reported.ShouldBeFalse("a supervisor that finished before the second caller reached it is not a disconnect failure");
    }

    /// <summary>
    /// Short, so the races below churn through connects and teardowns rather than sitting in the retry delay.
    /// </summary>
    static readonly TimeSpan ShortRetry = TimeSpan.FromMilliseconds(20);

    record TestSubscriptionOptions : SubscriptionOptions;

    /// <summary>
    /// Counts its connects and hands out the run to fail; the run is kept past its own teardown on purpose,
    /// since failing a retired run is exactly what these tests are about.
    /// </summary>
    class TestSubscription(TestSubscriptionOptions options, ConsumePipe pipe, ILoggerFactory loggerFactory)
        : EventSubscription<TestSubscriptionOptions>(options, pipe, loggerFactory, null) {
        int              _connects;
        SubscriptionRun? _run;

        public int Connects => Volatile.Read(ref _connects);

        public void Fail() => Volatile.Read(ref _run)?.Fail(DropReason.SubscriptionError, new InvalidOperationException("Simulated failure during shutdown"));

        protected override ValueTask Connect(SubscriptionRun run) {
            Volatile.Write(ref _run, run);
            Interlocked.Increment(ref _connects);

            return default;
        }
    }

}
