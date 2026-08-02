using System.Collections.Concurrent;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// A dropped subscription schedules the resubscribe on a background task, and that task used to read
/// <c>Stopping.Token</c> long after <c>Unsubscribe</c> got to the source. A drop that races shutdown is
/// benign — there is nothing left to resubscribe to — but it used to cost a spurious warning, an
/// unobserved exception, and a commit-handler dispose racing the one in <c>Finalize</c> (AI-1699).
/// </summary>
public class SubscriptionShutdownTests {
    /// <summary>
    /// The KurrentDB subscriptions cancel <c>Stopping</c> at the top of their <c>Unsubscribe</c>, so a
    /// drop during shutdown normally finds the token cancelled. Resubscribing from there is pure waste:
    /// <c>EventSubscriptionWithCheckpoint.Resubscribe</c> disposes the commit handler before it ever
    /// looks at the token, which is what put a second disposer in the race with <c>Finalize</c>.
    /// </summary>
    [Test]
    public async Task Drop_after_shutdown_started_does_not_resubscribe(CancellationToken ct) {
        var subscription = new TestSubscription(new() { SubscriptionId = "test-drop-when-cancelled" }, new ConsumePipe(), new CapturingLoggerFactory());

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        subscription.DropAfterCancellingStopping();

        var resubscribed = await subscription.WaitForResubscribe(TimeSpan.FromSeconds(2));

        resubscribed.ShouldBeFalse("a subscription that is already stopping has nothing to resubscribe to");
    }

    [Test]
    public async Task Drop_racing_unsubscribe_does_not_report_a_disposed_cts(CancellationToken ct) {
        var logs = new CapturingLoggerFactory();

        var subscription = new TestSubscription(new() { SubscriptionId = "test-drop-race" }, new ConsumePipe(), logs);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // Reproduce the losing side of the race: Unsubscribe has already disposed Stopping while the
        // subscription still believes it's running, which is exactly what lets Dropped reach the token.
        subscription.DropAfterDisposingStopping();

        var reported = await logs.WaitForWarning("CancellationTokenSource has been disposed", TimeSpan.FromSeconds(2));

        reported.ShouldBeFalse("dropping while Unsubscribe disposes Stopping is a benign shutdown race, not an error");
    }

    record TestSubscriptionOptions : SubscriptionOptions;

    /// <summary>
    /// A subscription that does nothing but expose the drop path. <c>Stopping</c> is protected, so both
    /// shutdown states can be reproduced without any test-only hooks in the production class.
    /// </summary>
    class TestSubscription(TestSubscriptionOptions options, ConsumePipe pipe, ILoggerFactory loggerFactory)
        : EventSubscription<TestSubscriptionOptions>(options, pipe, loggerFactory, null) {
        readonly TaskCompletionSource _resubscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void DropAfterCancellingStopping() {
            Stopping.Cancel(false);
            Drop();
        }

        public void DropAfterDisposingStopping() {
            Stopping.Dispose();
            Drop();
        }

        public async Task<bool> WaitForResubscribe(TimeSpan timeout)
            => await Task.WhenAny(_resubscribed.Task, Task.Delay(timeout)) == _resubscribed.Task;

        protected override Task Resubscribe(TimeSpan delay, CancellationToken cancellationToken) {
            _resubscribed.TrySetResult();

            return Task.CompletedTask;
        }

        protected override ValueTask Subscribe(CancellationToken cancellationToken) => default;

        protected override ValueTask Unsubscribe(CancellationToken cancellationToken) => default;

        void Drop() => Dropped(DropReason.SubscriptionError, new InvalidOperationException("Simulated drop during shutdown"));
    }

    sealed class CapturingLoggerFactory : ILoggerFactory {
        readonly ConcurrentQueue<string> _warnings = [];

        /// <summary>
        /// Polls rather than waiting out the full timeout, so the failing case reports in milliseconds.
        /// The resubscribe runs on a fire-and-forget task, so there is nothing to await on.
        /// </summary>
        public async Task<bool> WaitForWarning(string contains, TimeSpan timeout) {
            var deadline = DateTime.UtcNow + timeout;

            while (DateTime.UtcNow < deadline) {
                if (_warnings.Any(w => w.Contains(contains))) return true;

                await Task.Delay(20);
            }

            return false;
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(_warnings);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        sealed class CapturingLogger(ConcurrentQueue<string> warnings) : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
                if (logLevel >= LogLevel.Warning) warnings.Enqueue(formatter(state, exception));
            }
        }
    }
}
