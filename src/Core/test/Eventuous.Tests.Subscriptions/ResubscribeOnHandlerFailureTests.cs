using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Shouldly;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Tests that verify the subscription properly triggers resubscription when a handler throws an exception.
/// This reproduces the issue described in GitHub issue #407 where exceptions in handlers cause the subscription
/// to silently stop without triggering the Dropped/Resubscribe flow.
/// </summary>
public class ResubscribeOnHandlerFailureTests {
    [Test]
    public async Task Should_trigger_dropped_when_handler_throws_with_throw_on_error(CancellationToken ct) {
        // Arrange
        var loggerFactory   = LoggingExtensions.GetLoggerFactory();
        var droppedTcs      = new TaskCompletionSource<(string Id, DropReason Reason, Exception? Ex)>();
        var checkpointTcs   = new TaskCompletionSource<Checkpoint>();
        var subscribedCount = 0;

        var options = new TestSubscriptionOptions {
            SubscriptionId = "test-handler-failure",
            ThrowOnError   = true
        };

        var handler = new FailingHandler(failOnEvent: 2);
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var checkpointStore = new NoOpCheckpointStore();

        // Track checkpoint commits — the flush happens asynchronously during dispose
        checkpointStore.CheckpointStored += (_, cp) => checkpointTcs.TrySetResult(cp);

        var subscription = new TestPollingSubscription(
            options,
            checkpointStore,
            pipe,
            loggerFactory,
            eventCount: 5
        );

        // Act
        await subscription.Subscribe(
            _ => Interlocked.Increment(ref subscribedCount),
            (id, reason, ex) => droppedTcs.TrySetResult((id, reason, ex)),
            ct
        );

        // Wait for the subscription to either drop or time out
        var completedTask = await Task.WhenAny(droppedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10), ct));

        // Assert
        if (completedTask == droppedTcs.Task) {
            var (id, _, _) = await droppedTcs.Task;
            id.ShouldBe("test-handler-failure");
            // Subscription should have been dropped due to error
            subscription.IsDropped.ShouldBeTrue("Subscription should be marked as dropped after handler failure");
        }
        else {
            var handledCount = handler.HandledCount;

            Assert.Fail(
                $"Dropped was never called. Handler processed {handledCount} events before failure. " +
                $"IsRunning={subscription.IsRunning}, IsDropped={subscription.IsDropped}. "           +
                "This confirms the bug: exception in handler causes silent subscription death."
            );
        }

        // Cleanup
        await subscription.Unsubscribe(_ => { }, ct);

        // Wait for the checkpoint to be committed — the flush may still be in flight
        // from the Resubscribe path disposing the handler asynchronously
        var checkpointCommitted = await Task.WhenAny(checkpointTcs.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));
        checkpointCommitted.ShouldBe(checkpointTcs.Task, "Checkpoint should have been committed during handler disposal");

        // Verify: only event #1 (position 0) was successfully acked before the failure on event #2
        var checkpoint = await checkpointTcs.Task;
        checkpoint.Position.ShouldBe((ulong)0, "Checkpoint should be at position 0 (only the first event was acked before failure)");
    }

    [Test]
    public async Task Should_skip_failed_event_and_advance_checkpoint_when_throw_on_error_disabled(CancellationToken ct) {
        // Arrange — ThrowOnError = false means Nack calls Ack (skip), so all events are processed
        var loggerFactory    = LoggingExtensions.GetLoggerFactory();
        var completedTcs     = new TaskCompletionSource();
        ulong? lastCommitted = null;
        var    commitTcs     = new TaskCompletionSource<ulong>();

        var options = new TestSubscriptionOptions {
            SubscriptionId          = "test-handler-skip",
            ThrowOnError            = false,
            CheckpointCommitBatchSize = 1,
            CheckpointCommitDelayMs   = 100
        };

        var handler = new FailingHandler(failOnEvent: 2);
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var checkpointStore = new NoOpCheckpointStore();

        // Track the highest committed position
        checkpointStore.CheckpointStored += (_, cp) => {
            if (cp.Position is { } pos) {
                lastCommitted = pos;

                if (pos >= 4) commitTcs.TrySetResult(pos);
            }
        };

        var subscription = new TestPollingSubscription(
            options,
            checkpointStore,
            pipe,
            loggerFactory,
            eventCount: 5,
            onCompleted: () => completedTcs.TrySetResult()
        );

        // Act
        await subscription.Subscribe(
            _ => { },
            (_, _, _) => { },
            ct
        );

        // Wait for all events to be processed
        var completed = await Task.WhenAny(completedTcs.Task, Task.Delay(TimeSpan.FromSeconds(10), ct));
        completed.ShouldBe(completedTcs.Task, "All events should be processed when ThrowOnError is false");

        // Cleanup — Finalize flushes pending checkpoint commits
        await subscription.Unsubscribe(_ => { }, ct);

        // Wait for checkpoint to reach the last event position
        var commitCompleted = await Task.WhenAny(commitTcs.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));
        commitCompleted.ShouldBe(commitTcs.Task, $"Checkpoint should reach position 4, last committed: {lastCommitted}");

        // Verify: checkpoint should have advanced past all events including the failed one (which was skipped)
        lastCommitted.ShouldBe((ulong)4, "Checkpoint should be at position 4 (all events processed, failed one skipped)");
    }

    /// <summary>
    /// A handler that throws an exception when processing a specific event number.
    /// </summary>
    class FailingHandler(int failOnEvent) : BaseEventHandler {
        public int HandledCount;

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            var count = Interlocked.Increment(ref HandledCount);

            return count == failOnEvent ? throw new InvalidOperationException($"Simulated handler failure on event #{count}") : new(EventHandlingStatus.Success);
        }
    }

    /// <summary>
    /// Validates that Ack does not throw when CheckpointCommitHandler is concurrently
    /// nulled by Resubscribe/DisposeCommitHandler on another thread while the
    /// AsyncHandlingFilter worker is still completing a message.
    /// </summary>
    [Test]
    [Retry(3)]
    public async Task Should_not_throw_nre_when_ack_races_with_resubscribe(CancellationToken ct) {
        // Arrange
        var loggerFactory = LoggingExtensions.GetLoggerFactory();
        var nreTcs        = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var ackStarted    = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceedToAck  = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = new TestSubscriptionOptions {
            SubscriptionId            = "test-ack-race",
            ThrowOnError              = true,
            CheckpointCommitBatchSize = 1,
            CheckpointCommitDelayMs   = 100
        };

        // A handler that signals when it's about to ack, then waits for the test to
        // trigger resubscribe before the ack path runs.
        var handler = new SlowAckHandler(ackStarted, proceedToAck);
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var checkpointStore = new NoOpCheckpointStore();

        var subscription = new TestPollingSubscription(
            options,
            checkpointStore,
            pipe,
            loggerFactory,
            eventCount: 20
        );

        // Act
        await subscription.Subscribe(
            _ => { },
            (_, _, ex) => {
                if (ex is NullReferenceException nre) nreTcs.TrySetResult(nre);
            },
            ct
        );

        // Wait until the handler has processed an event and is about to ack
        var started = await Task.WhenAny(ackStarted.Task, Task.Delay(TimeSpan.FromSeconds(10), ct));
        started.ShouldBe(ackStarted.Task, "Handler should have started processing an event");

        // Now trigger Dropped → Resubscribe, which will null CheckpointCommitHandler
        subscription.TriggerDropped();

        // Give Resubscribe a moment to dispose the commit handler
        await Task.Delay(200, ct);

        // Let the handler complete — the AsyncHandlingFilter worker will now call Acknowledge → Ack.
        // Without the fix, the commit handler is already null at this point, causing an NRE.
        proceedToAck.TrySetResult();

        // Assert — wait for either the NRE or a timeout
        var result = await Task.WhenAny(nreTcs.Task, Task.Delay(TimeSpan.FromSeconds(5), ct));

        if (result == nreTcs.Task) {
            var exception = await nreTcs.Task;
            Assert.Fail(
                $"NullReferenceException in Ack path during resubscribe race: {exception}. " +
                "CheckpointCommitHandler was null when Ack tried to call Commit()."
            );
        }

        // Cleanup
        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// A handler that signals the test when processing is happening,
    /// then blocks until the test allows it to complete. This creates the
    /// window for the race between Ack and Resubscribe.
    /// </summary>
    class SlowAckHandler(TaskCompletionSource ackStarted, TaskCompletionSource proceedToAck) : BaseEventHandler {
        int _signaled;

        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            // Signal only on the first event to avoid double-signaling
            if (Interlocked.CompareExchange(ref _signaled, 1, 0) == 0) {
                ackStarted.TrySetResult();
                await proceedToAck.Task;
            }

            return EventHandlingStatus.Success;
        }
    }

    record TestSubscriptionOptions : SubscriptionWithCheckpointOptions;

    /// <summary>
    /// A minimal polling subscription that generates synthetic events and sends them
    /// through the full pipeline (including AsyncHandlingFilter).
    /// </summary>
    class TestPollingSubscription(
            TestSubscriptionOptions options,
            ICheckpointStore        checkpointStore,
            ConsumePipe             pipe,
            ILoggerFactory?         loggerFactory,
            int                     eventCount,
            Action?                 onCompleted = null
        )
        : EventSubscriptionWithCheckpoint<TestSubscriptionOptions>(
            options,
            checkpointStore,
            pipe,
            1,
            SubscriptionKind.All,
            loggerFactory,
            null,
            null
        ) {
        TaskRunner? _runner;

        /// <summary>
        /// Exposes the protected Dropped method so the test can trigger a resubscribe.
        /// </summary>
        public void TriggerDropped()
            => Dropped(DropReason.SubscriptionError, new InvalidOperationException("Simulated drop for race test"));

        protected override ValueTask Subscribe(CancellationToken cancellationToken) {
            _runner = new TaskRunner(token => PollEvents(token)).Start();

            return default;
        }

        protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
            if (_runner == null) return;

            await _runner.Stop(cancellationToken);
            _runner.Dispose();
            _runner = null;
        }

        async Task PollEvents(CancellationToken cancellationToken) {
            var checkpoint = await GetCheckpoint(cancellationToken);
            var start = (int)(checkpoint.Position ?? 0);

            for (var i = start; i < eventCount && !cancellationToken.IsCancellationRequested; i++) {
                var context = new MessageConsumeContext(
                    Guid.NewGuid().ToString(),
                    "TestEvent",
                    "application/json",
                    "test-stream",
                    (ulong)i,
                    (ulong)i,
                    (ulong)i,
                    Sequence++,
                    DateTime.UtcNow,
                    new { EventNumber = i },
                    new(),
                    Options.SubscriptionId,
                    cancellationToken
                ) { LogContext = Log };

                await HandleInternal(context).NoContext();

                await Task.Delay(50, cancellationToken);
            }

            onCompleted?.Invoke();
        }
    }
}
