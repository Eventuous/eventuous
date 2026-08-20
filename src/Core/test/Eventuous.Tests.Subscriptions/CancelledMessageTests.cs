using System.Collections.Concurrent;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Shouldly;
using LoggingExtensions = Eventuous.TestHelpers.TUnit.Logging.LoggingExtensions;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// Pins down the fix in <see cref="AsyncHandlingFilter"/>.<c>DelayedConsume</c>: whether a cancelled handler
/// gets acknowledged must turn on whose token was cancelled, not on the exception type. Before the fix, any
/// <see cref="OperationCanceledException"/> was acknowledged regardless of cause, silently skipping the
/// event in flight when a run's own teardown cancelled a parked handler.
/// </summary>
public class CancelledMessageTests {
    /// <summary>
    /// A handler cancelled because the run is ending was never given a verdict, so it must not be
    /// acknowledged — only redelivered once the successor run comes up.
    /// </summary>
    [Test]
    public async Task Handler_cancelled_by_shutdown_is_not_acknowledged_and_is_redelivered(CancellationToken ct) {
        var loggerFactory   = LoggingExtensions.GetLoggerFactory();
        var checkpointStore = new NoOpCheckpointStore();
        var committed       = new ConcurrentQueue<ulong?>();
        checkpointStore.CheckpointStored += (_, cp) => committed.Enqueue(cp.Position);

        var handler = new ParkOnFirstDeliveryHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var options = new TestOptions {
            SubscriptionId            = "cancelled-not-acked",
            ThrowOnError              = true,
            CheckpointCommitBatchSize = 1,
            CheckpointCommitDelayMs   = 10
        };

        // The default retry delay is 2s; a short one keeps the test deterministic and fast.
        options.RetryDelay = TimeSpan.FromMilliseconds(20);

        var subscription = new SingleEventSubscription(options, checkpointStore, pipe, loggerFactory);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await Wait.Until(() => handler.Parked.IsCompleted, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the handler should have received the first delivery and parked on it");

        committed.ShouldBeEmpty("nothing should commit while the only delivery so far is still parked, undecided");

        subscription.FailCurrentRun();

        // The handler blocks again on redelivery, so the test can inspect the checkpoint before it's allowed to succeed.
        (await Wait.Until(() => handler.RedeliveryStarted.IsCompleted, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the successor run should redeliver the event the cancelled handler never finished");

        // If the fix regresses, DelayedConsume acknowledges the cancelled delivery and this fires.
        committed.ShouldBeEmpty("the checkpoint must never move past an event whose only delivery was cancelled by shutdown, not decided");

        handler.LetRedeliverySucceed();

        (await Wait.Until(() => committed.Contains((ulong?)0), TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("the checkpoint should reach position 0 once the redelivered event is actually handled");

        await subscription.Unsubscribe(_ => { }, ct);
    }

    /// <summary>
    /// An <see cref="OperationCanceledException"/> from the handler's own token (e.g. an HttpClient timeout)
    /// is an ordinary failure, not a shutdown — it must be skipped like any other exception, not left
    /// unacknowledged. Guards against fixing the test above by matching on exception type instead of on
    /// which token fired.
    /// </summary>
    [Test]
    public async Task Handler_self_cancellation_is_an_ordinary_failure_and_is_skipped(CancellationToken ct) {
        var loggerFactory   = LoggingExtensions.GetLoggerFactory();
        var checkpointStore = new NoOpCheckpointStore();
        var committed       = new ConcurrentQueue<ulong?>();
        checkpointStore.CheckpointStored += (_, cp) => committed.Enqueue(cp.Position);

        var handler = new SelfCancellingHandler();
        var pipe    = new ConsumePipe().AddDefaultConsumer(handler);

        var options = new TestOptions {
            SubscriptionId            = "self-cancel-is-ordinary-failure",
            ThrowOnError              = false,
            CheckpointCommitBatchSize = 1,
            CheckpointCommitDelayMs   = 10
        };

        var subscription = new SingleEventSubscription(options, checkpointStore, pipe, loggerFactory);

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await Wait.Until(() => committed.Contains((ulong?)0), TimeSpan.FromSeconds(5)))
            .ShouldBeTrue("a handler-local cancellation should be skipped and the checkpoint should advance past it");

        await subscription.Unsubscribe(_ => { }, ct);
    }



    record TestOptions : SubscriptionWithCheckpointOptions;

    /// <summary>
    /// Delivers one synthetic event at position 0, once per run, then parks until the run ends.
    /// </summary>
    sealed class SingleEventSubscription(
            TestOptions      options,
            ICheckpointStore checkpointStore,
            ConsumePipe      pipe,
            ILoggerFactory?  loggerFactory
        )
        : EventSubscriptionWithCheckpoint<TestOptions>(
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

        /// <summary>
        /// Fails the current run, standing in for a transport drop or any other reason the supervisor tears a run down.
        /// </summary>
        public void FailCurrentRun()
            => Volatile.Read(ref _run)?.Fail(DropReason.SubscriptionError, new InvalidOperationException("Simulated drop while a handler is parked"));

        protected override async ValueTask Connect(SubscriptionRun run) {
            Volatile.Write(ref _run, run);

            await GetCheckpoint(run).NoContext();

            // Started on a task of its own so it never runs inline on the supervisor's stack during Connect.
            var pumping = Task.Run(() => RunDeliverOnce(run), CancellationToken.None);

            // No handle of its own to release: registered purely to join the loop before the next Connect.
            run.OnDisconnect(_ => new(pumping));
        }

        /// <summary>
        /// Runs <see cref="DeliverOnce"/> and reports its own death, the same contract a real transport keeps.
        /// </summary>
        Task RunDeliverOnce(SubscriptionRun run)
            => TransportPump.Run(run, () => DeliverOnce(run), "SingleEventSubscription pump ended while the connection was up");

        async Task DeliverOnce(SubscriptionRun run) {
            var context = new MessageConsumeContext(
                Guid.NewGuid().ToString(),
                "TestEvent",
                "application/json",
                "test-stream",
                0,
                0,
                0,
                run.NextSequence(),
                DateTime.UtcNow,
                new { EventNumber = 0 },
                new(),
                Options.SubscriptionId,
                run.Token
            ) { LogContext = Log };

            await HandleInternal(run, context).NoContext();

            // Parked rather than returned: a pump ending while its connection is up is read as a drop.
            await run.Ended.NoContext();
        }
    }

    /// <summary>
    /// Parks on the first delivery until cancelled, then parks again on redelivery so the test can inspect
    /// the checkpoint before the second attempt succeeds.
    /// </summary>
    sealed class ParkOnFirstDeliveryHandler : BaseEventHandler {
        readonly TaskCompletionSource _neverCompletes     = new();
        readonly TaskCompletionSource _parked             = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _redeliveryStarted  = new(TaskCreationOptions.RunContinuationsAsynchronously);
        readonly TaskCompletionSource _proceedWithSuccess = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int _deliveries;

        public Task Parked            => _parked.Task;
        public Task RedeliveryStarted => _redeliveryStarted.Task;

        public void LetRedeliverySucceed() => _proceedWithSuccess.TrySetResult();

        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            var attempt = Interlocked.Increment(ref _deliveries);

            switch (attempt) {
                case 1:
                    _parked.TrySetResult();
                    await _neverCompletes.Task.WaitAsync(context.CancellationToken).NoContext();
                    break;
                case 2:
                    _redeliveryStarted.TrySetResult();
                    await _proceedWithSuccess.Task.NoContext();
                    break;
            }

            return EventHandlingStatus.Success;
        }
    }

    /// <summary>
    /// Fails the way an <c>HttpClient</c> call does on a timeout: an <see cref="OperationCanceledException"/>
    /// whose token belongs to the failing operation, not to anything the subscription owns.
    /// </summary>
    sealed class SelfCancellingHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context)
            => throw new TaskCanceledException("Simulated HttpClient timeout");
    }
}
