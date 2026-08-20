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
            // Reaching the drop callback at all is the assertion: it is the only report of a drop there is.
            var (id, _, _) = await droppedTcs.Task;
            id.ShouldBe("test-handler-failure");
        }
        else {
            var handledCount = handler.HandledCount;

            Assert.Fail(
                $"Dropped was never called. Handler processed {handledCount} events before failure. " +
                $"IsRunning={subscription.IsRunning}, subscribed {subscribedCount} time(s). "         +
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

    // A test watching OnDropped for an NRE on an ack/teardown race used to live here, but was unreachable
    // (SubscriptionRun.Fail keeps only the first reason). The invariant it meant to cover is now asserted in
    // ResubscribeConcurrencyTests.An_acknowledgement_from_a_dropped_run_is_refused.

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
        SubscriptionRun? _run;

        protected override async ValueTask Connect(SubscriptionRun run) {
            Volatile.Write(ref _run, run);

            var checkpoint = await GetCheckpoint(run).NoContext();

            // Started on a task of its own so it never runs inline on the supervisor's stack during Connect.
            var pumping = Task.Run(() => RunPollEvents(run, (int)(checkpoint.Position ?? 0)), CancellationToken.None);

            // No handle of its own to release: registered purely to join the loop before the next Connect.
            run.OnDisconnect(_ => new(pumping));
        }

        /// <summary>
        /// Runs <see cref="PollEvents"/> and reports its own death, the same contract a real transport keeps.
        /// </summary>
        Task RunPollEvents(SubscriptionRun run, int start)
            => TransportPump.Run(run, () => PollEvents(run, start), "TestPollingSubscription pump ended while the connection was up");

        async Task PollEvents(SubscriptionRun run, int start) {
            for (var i = start; i < eventCount && !run.Token.IsCancellationRequested; i++) {
                var context = new MessageConsumeContext(
                    Guid.NewGuid().ToString(),
                    "TestEvent",
                    "application/json",
                    "test-stream",
                    (ulong)i,
                    (ulong)i,
                    (ulong)i,
                    run.NextSequence(),
                    DateTime.UtcNow,
                    new { EventNumber = i },
                    new(),
                    Options.SubscriptionId,
                    run.Token
                ) { LogContext = Log };

                await HandleInternal(run, context).NoContext();

                await Task.Delay(50, run.Token).NoContext();
            }

            onCompleted?.Invoke();

            // Parked rather than returned: a pump ending while its connection is up is read as a drop.
            await run.Ended.NoContext();
        }
    }
}
