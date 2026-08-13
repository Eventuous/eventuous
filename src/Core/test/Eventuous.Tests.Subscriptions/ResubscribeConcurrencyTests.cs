using System.Collections.Concurrent;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace Eventuous.Tests.Subscriptions;

/// <summary>
/// One failure has to cost one resubscribe. A transport drop reaches Dropped once for the pump's read
/// and once for every in-flight message on the same dead connection; a handler deferring by throwing
/// under ThrowOnError produces the same burst. Either way: one drop cycle, one live transport, no
/// accumulation, and a checkpoint that keeps moving afterwards.
/// </summary>
public class ResubscribeConcurrencyTests {
    /// <summary>
    /// A whole page of messages fails while the pump keeps delivering, so Dropped is called once per
    /// failing message, all inside one drop window.
    /// </summary>
    [Test]
    public async Task Burst_of_nacks_produces_a_single_resubscribe(CancellationToken ct) {
        var logs = new RecordingLoggerFactory();

        const int messageCount = 8;

        var handler = new DeferringHandler(_ => true);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "burst-of-nacks",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: 4,
            // Only the first run delivers: a replacement redelivering the same failing messages would
            // legitimately open a second drop window.
            pump: async (sub, transport, start, token) => {
                if (transport.Index == 0) {
                    for (var i = 0; i < messageCount; i++) await sub.Deliver(start + (ulong)i, token).NoContext();
                }

                await Task.Delay(Timeout.Infinite, token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(500) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // All of them have to fail before the resubscribe fires, or this proves nothing about
        // concurrent drops.
        (await WaitUntil(() => handler.HandledCount >= messageCount, TimeSpan.FromSeconds(5)))
            .ShouldBeTrue($"all {messageCount} messages should have been handled and nacked, got {handler.HandledCount}");

        (await WaitUntil(() => subscription.SubscribeCalls > 1, TimeSpan.FromSeconds(5))).ShouldBeTrue("the subscription should have resubscribed");

        // Give any extra resubscribes scheduled by the other nacks time to show up before asserting.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);

        await subscription.Unsubscribe(_ => { }, ct);

        subscription.SubscribeCalls.ShouldBe(2, "one initial subscribe plus exactly one resubscribe for the whole drop cycle");
        logs.Count("Resubscribing").ShouldBe(1, "a burst of nacks is one drop cycle, so it gets one 'Resubscribing' line");
        logs.Count("Dropped:").ShouldBe(1, "the drop is reported once per cycle, not once per failing message");
    }

    /// <summary>
    /// No handler involvement at all: the connection dies, so the pump's read throws and every in-flight
    /// operation on it fails at the same moment. One transport failure, one resubscribe.
    /// </summary>
    [Test]
    public async Task Transport_failure_produces_a_single_resubscribe(CancellationToken ct) {
        const int inFlight = 4;
        const ulong last = 7;

        var logs    = new RecordingLoggerFactory();
        var store   = new NoOpCheckpointStore();
        var handler = new ConnectionBoundHandler(t => t == 0);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "transport-failure",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            logs,
            concurrencyLimit: inFlight,
            pump: async (sub, transport, start, token) => {
                if (transport.Index > 0) {
                    // The store is back. Nothing about this run is unusual.
                    for (var i = start; i <= last && !token.IsCancellationRequested; i++) await sub.Deliver(i, token, transport.Index).NoContext();

                    await Task.Delay(Timeout.Infinite, token).NoContext();

                    return;
                }

                for (var i = 0; i < inFlight; i++) await sub.Deliver((ulong)i, token, transport.Index).NoContext();

                // Wait until they're in flight, so the failure hits all of them at once.
                await WaitUntil(() => handler.InFlight == inFlight, TimeSpan.FromSeconds(5)).NoContext();

                // HTTP/2 INTERNAL_ERROR: the connection is gone, and everything using it fails together.
                handler.KillConnection(transport.Index);

                // What AllStreamSubscription.PumpMessages does when MoveNextAsync throws.
                sub.Drop(new IOException("The HTTP/2 server closed the connection"));

                await Task.Delay(Timeout.Infinite, token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(300) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // (c) The checkpoint has to keep moving afterwards.
        var recovered = await WaitUntil(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(15)
        );

        // Let any resubscribe scheduled by the other nacks arrive before counting.
        await Task.Delay(TimeSpan.FromSeconds(1), ct);
        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue("the subscription must commit again after the connection comes back");

        // (a) One transport failure, one drop cycle, one resubscribe.
        subscription.SubscribeCalls.ShouldBe(2, "one initial subscribe plus exactly one resubscribe for the whole connection failure");
        logs.Count("Resubscribing").ShouldBe(1, "the pump's drop and the in-flight nacks are one drop cycle between them");
        logs.Count("Dropped:").ShouldBe(1, "the drop is reported once per cycle, not once per failed operation");

        // (b) The run that owned the dead connection is gone.
        var transports = subscription.Transports.ToArray();
        transports[0].IsDisposed.ShouldBeTrue("the dead connection should have been disposed");
        transports[0].PumpExited.ShouldBeTrue("the pump reading the dead connection should have exited");
        subscription.MaxLivePumps.ShouldBe(1, "there should never be more than one message pump alive at a time");
    }

    /// <summary>
    /// The same failure over and over — a rolling restart, or a flapping node. Nothing may accumulate,
    /// and the checkpoint has to end up where it would have without any of it.
    /// </summary>
    [Test]
    public async Task Repeated_transport_failures_do_not_accumulate(CancellationToken ct) {
        const int cycles = 25;
        const int inFlight = 4;
        const ulong last = 7;

        var store   = new NoOpCheckpointStore();
        var handler = new ConnectionBoundHandler(t => t < cycles);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "repeated-transport-failure",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new RecordingLoggerFactory(),
            concurrencyLimit: inFlight,
            pump: async (sub, transport, start, token) => {
                if (transport.Index >= cycles) {
                    // The store settles down and the subscription catches up.
                    for (var i = start; i <= last && !token.IsCancellationRequested; i++) await sub.Deliver(i, token, transport.Index).NoContext();

                    await Task.Delay(Timeout.Infinite, token).NoContext();

                    return;
                }

                for (var i = start; i < start + inFlight && !token.IsCancellationRequested; i++) await sub.Deliver(i, token, transport.Index).NoContext();

                await WaitUntil(() => handler.InFlight == inFlight, TimeSpan.FromSeconds(5)).NoContext();
                handler.KillConnection(transport.Index);
                sub.Drop(new IOException("The HTTP/2 server closed the connection"));

                await Task.Delay(Timeout.Infinite, token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(10) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        var recovered = await WaitUntil(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(60)
        );

        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue($"the checkpoint should have caught up after {cycles} connection failures");

        subscription.SubscribeCalls.ShouldBe(cycles + 1, "each connection failure should cost exactly one resubscribe");
        subscription.MaxLivePumps.ShouldBe(1, "message pumps accumulated across connection failures");
        subscription.Transports.Count(t => !t.IsDisposed).ShouldBe(0, "every dead connection should have been disposed");
        subscription.Transports.Count.ShouldBe(subscription.SubscribeCalls, "each subscribe should create exactly one connection");
    }

    /// <summary>
    /// The dropped run's pump must be gone before the next one starts. An orphan keeps reading into the
    /// same consume pipe, and every message it delivers is a duplicate.
    /// </summary>
    [Test]
    public async Task Resubscribe_stops_the_previous_transport_and_pump(CancellationToken ct) {
        var deliveries = new ConcurrentQueue<(int Transport, ulong Position)>();

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "stop-previous-pump",
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            new RecordingLoggerFactory(),
            concurrencyLimit: 1,
            pump: async (sub, transport, start, token) => {
                var position = start;

                while (!token.IsCancellationRequested) {
                    deliveries.Enqueue((transport.Index, position));
                    await sub.Deliver(position++, token).NoContext();
                    await Task.Delay(5, token).NoContext();
                }
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(100) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await WaitUntil(() => deliveries.Count > 3, TimeSpan.FromSeconds(5))).ShouldBeTrue("the first pump should be delivering");

        subscription.Drop(new InvalidOperationException("Simulated transport drop"));

        (await WaitUntil(() => deliveries.Any(d => d.Transport == 1), TimeSpan.FromSeconds(5))).ShouldBeTrue("the replacement pump should be delivering");

        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        await subscription.Unsubscribe(_ => { }, ct);

        var transports = subscription.Transports.ToArray();
        transports.Length.ShouldBe(2);

        transports[0].IsDisposed.ShouldBeTrue("the dropped transport should have been disposed before the new one was created");
        transports[0].PumpExited.ShouldBeTrue("the dropped run's message pump should have exited");

        // Once the replacement pump has delivered anything, the old one must never be heard from again.
        var recorded    = deliveries.ToArray();
        var firstOnNew  = Array.FindIndex(recorded, d => d.Transport == 1);
        var afterSwitch = recorded.Skip(firstOnNew);

        afterSwitch.ShouldAllBe(d => d.Transport == 1, "two live pumps were dispatching into the same pipe");
        subscription.MaxLivePumps.ShouldBe(1, "there should never be more than one message pump alive at a time");
    }

    /// <summary>
    /// Two contexts sharing a sequence number collapse into one entry in the commit handler's position
    /// set, which then refuses to commit past the resulting hole.
    /// </summary>
    [Test]
    public async Task Concurrent_context_creation_yields_unique_sequences() {
        const int threads = 8;
        const int perThread = 2000;

        var subscription = new PumpingSubscription(
            new() { SubscriptionId = "unique-sequences" },
            new NoOpCheckpointStore(),
            new ConsumePipe().AddDefaultConsumer(new CountingHandler()),
            new RecordingLoggerFactory(),
            concurrencyLimit: 1,
            pump: (_, _, _, token) => Task.Delay(Timeout.Infinite, token)
        );

        var results = new ulong[threads][];

        await Task.WhenAll(
            Enumerable.Range(0, threads)
                .Select(t => Task.Run(() => {
                            var mine = new ulong[perThread];

                            for (var i = 0; i < perThread; i++) mine[i] = subscription.CreateContext(0).Sequence;

                            results[t] = mine;
                        }
                    )
                )
        );

        var all = results.SelectMany(x => x).ToArray();

        all.Length.ShouldBe(threads * perThread);
        all.Distinct().Count().ShouldBe(all.Length, "sequence numbers must be unique across concurrent context creation");
        all.Order().ShouldBe(Enumerable.Range(0, all.Length).Select(i => (ulong)i), "sequence numbers must be a gapless monotonic run");

        // Each thread must see its own values increase, so the sequence is monotonic per producer too.
        foreach (var mine in results) mine.ShouldBeInOrder(SortDirection.Ascending);
    }

    /// <summary>
    /// The steady state of a deferring handler: the same message fails on every redelivery, the
    /// checkpoint holds at the last one before it, and nothing accumulates while that goes on.
    /// </summary>
    [Test]
    public async Task Repeated_nacks_hold_a_stable_checkpoint_without_accumulating(CancellationToken ct) {
        const int cycles = 100;
        const ulong failAt = 3;

        var stored = new ConcurrentQueue<ulong?>();
        var store  = new NoOpCheckpointStore();
        store.CheckpointStored += (_, cp) => stored.Enqueue(cp.Position);

        var handler = new DeferringHandler(context => context.GlobalPosition >= failAt);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "stable-checkpoint",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new RecordingLoggerFactory(),
            concurrencyLimit: 1,
            pump: async (sub, _, start, token) => {
                for (var i = start; i < start + 5 && !token.IsCancellationRequested; i++) await sub.Deliver(i, token).NoContext();

                await Task.Delay(Timeout.Infinite, token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(10) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        (await WaitUntil(() => subscription.SubscribeCalls > cycles, TimeSpan.FromSeconds(60)))
            .ShouldBeTrue($"the subscription should have gone through {cycles} drop cycles, it did {subscription.SubscribeCalls - 1}");

        await subscription.Unsubscribe(_ => { }, ct);

        // The failing message is never acked, so the checkpoint parks on the one before it. A corrupted
        // sequence shows up as a checkpoint that never commits, or one that jumps past the failure.
        stored.ShouldNotBeEmpty("the messages before the failing one should have been committed");
        stored.ShouldAllBe(p => p < failAt, "the checkpoint must never advance past the message that keeps failing");
        stored.Last().ShouldBe(failAt - 1, "the checkpoint should settle on the last message before the failing one");
        (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position.ShouldBe(failAt - 1);

        // Only the messages ahead of the failure, plus the flush when the first commit handler is
        // replaced. More than that means the checkpoint is being rewritten every cycle.
        stored.Count.ShouldBeLessThanOrEqualTo((int)failAt + 1, $"the checkpoint should settle, not be rewritten across {cycles} cycles");

        handler.HandledCount.ShouldBeGreaterThanOrEqualTo(cycles, "the failing message must be redelivered on every cycle");

        // Nothing accumulates: one transport and one pump at a time, no matter how many cycles.
        subscription.MaxLivePumps.ShouldBe(1, "message pumps accumulated across drop cycles");
        subscription.Transports.Count(t => !t.IsDisposed).ShouldBe(0, "every transport should have been disposed by the time the subscription stops");
        subscription.Transports.Count.ShouldBe(subscription.SubscribeCalls, "each subscribe should create exactly one transport");
    }

    /// <summary>
    /// Nothing commits past a gap in the sequence, so a subscription whose checkpoint stalled needs a
    /// clean commit handler from the next subscribe — or it processes forever without ever committing.
    /// </summary>
    [Test]
    public async Task Subscription_commits_again_after_a_commit_gap(CancellationToken ct) {
        const ulong failAt = 3;
        const ulong last = 7;

        var store = new NoOpCheckpointStore();
        var defer = true;

        // Fails on one message until the test lets it through, exactly like a precondition that resolves.
        var handler = new DeferringHandler(context => defer && context.GlobalPosition == failAt);

        var subscription = new PumpingSubscription(
            new() {
                SubscriptionId            = "recover-after-gap",
                ThrowOnError              = true,
                CheckpointCommitBatchSize = 1,
                CheckpointCommitDelayMs   = 10
            },
            store,
            new ConsumePipe().AddDefaultConsumer(handler),
            new RecordingLoggerFactory(),
            concurrencyLimit: 1,
            pump: async (sub, _, start, token) => {
                for (var i = start; i <= last && !token.IsCancellationRequested; i++) await sub.Deliver(i, token).NoContext();

                await Task.Delay(Timeout.Infinite, token).NoContext();
            }
        ) { ResubscribeDelay = TimeSpan.FromMilliseconds(50) };

        await subscription.Subscribe(_ => { }, (_, _, _) => { }, ct);

        // The messages after the failing one ack out of order, leaving a hole nothing commits past.
        var stalled = await WaitUntil(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == failAt - 1,
            TimeSpan.FromSeconds(10)
        );

        stalled.ShouldBeTrue("the checkpoint should have stalled just before the failing message");

        // The precondition resolves. Nothing else changes — no restart, no manual intervention.
        defer = false;

        var recovered = await WaitUntil(
            async () => (await store.GetLastCheckpoint(subscription.SubscriptionId, ct)).Position == last,
            TimeSpan.FromSeconds(15)
        );

        await subscription.Unsubscribe(_ => { }, ct);

        recovered.ShouldBeTrue("a subscription whose checkpoint stalled must commit again once the failing message succeeds");
    }

    static Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout) => WaitUntil(() => Task.FromResult(condition()), timeout);

    static async Task<bool> WaitUntil(Func<Task<bool>> condition, TimeSpan timeout) {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline) {
            if (await condition()) return true;

            await Task.Delay(20);
        }

        return await condition();
    }

    const string TransportKey = "transport";

    record TestOptions : SubscriptionWithCheckpointOptions;

    /// <summary>
    /// Stands in for a transport connection, recording only whether the subscription got rid of it and
    /// whether the pump reading it has finished.
    /// </summary>
    sealed class FakeTransport(int index) : IAsyncDisposable {
        public int  Index      { get; } = index;
        public bool IsDisposed { get; private set; }
        public bool PumpExited { get; set; }

        public ValueTask DisposeAsync() {
            IsDisposed = true;

            return default;
        }
    }

    /// <summary>
    /// Shaped like the real catch-up subscriptions: Subscribe creates a transport and hands it to a pump
    /// on its own task, Unsubscribe drops the transport and waits for the pump. Pump body per test.
    /// </summary>
    sealed class PumpingSubscription(
            TestOptions                                                              options,
            ICheckpointStore                                                         checkpointStore,
            ConsumePipe                                                              pipe,
            ILoggerFactory?                                                          loggerFactory,
            int                                                                      concurrencyLimit,
            Func<PumpingSubscription, FakeTransport, ulong, CancellationToken, Task> pump
        )
        : EventSubscriptionWithCheckpoint<TestOptions>(options, checkpointStore, pipe, concurrencyLimit, SubscriptionKind.All, loggerFactory, null, null) {
        readonly ConcurrentQueue<FakeTransport> _transports = [];

        FakeTransport? _transport;
        Task?          _pumpTask;
        int            _subscribeCalls;
        int            _livePumps;
        int            _maxLivePumps;

        public IReadOnlyCollection<FakeTransport> Transports     => _transports;
        public int                                SubscribeCalls => Volatile.Read(ref _subscribeCalls);
        public int                                MaxLivePumps   => Volatile.Read(ref _maxLivePumps);

        public TimeSpan ResubscribeDelay { get; init; } = TimeSpan.FromMilliseconds(100);

        public void Drop(Exception exception) => Dropped(DropReason.SubscriptionError, exception);

        public MessageConsumeContext CreateContext(ulong position, int transport = 0)
            => new MessageConsumeContext(
                    Guid.NewGuid().ToString(),
                    "TestEvent",
                    "application/json",
                    "test-stream",
                    position,
                    position,
                    position,
                    Sequence++,
                    DateTime.UtcNow,
                    new { Position = position },
                    new(),
                    Options.SubscriptionId,
                    CancellationToken.None
                ) { LogContext = Log }
                .WithItem(TransportKey, transport);

        public ValueTask Deliver(ulong position, CancellationToken cancellationToken, int transport = 0) {
            var context = CreateContext(position, transport);
            context.CancellationToken = cancellationToken;

            return HandleInternal(context);
        }

        // The production delays are 2s and 10s; a hundred cycles of those would take half an hour.
        protected override Task Resubscribe(TimeSpan delay, CancellationToken cancellationToken) => base.Resubscribe(ResubscribeDelay, cancellationToken);

        protected override async ValueTask Subscribe(CancellationToken cancellationToken) {
            var (_, position) = await GetCheckpoint(cancellationToken).NoContext();
            var start = position == null ? 0 : position.Value + 1;

            var transport = new FakeTransport(Interlocked.Increment(ref _subscribeCalls) - 1);
            _transports.Enqueue(transport);
            _transport = transport;

            _pumpTask = Task.Run(
                async () => {
                    TrackPumpStarted();

                    try { await pump(this, transport, start, cancellationToken).NoContext(); } catch (Exception) {
                        // The pump ending is what the tests observe, not how it ended
                    } finally {
                        Interlocked.Decrement(ref _livePumps);
                        transport.PumpExited = true;
                    }
                },
                CancellationToken.None
            );
        }

        protected override async ValueTask Unsubscribe(CancellationToken cancellationToken) {
            var transport = _transport;
            var pump      = _pumpTask;
            _transport = null;
            _pumpTask  = null;

            // As in the real subscriptions: cancelling a source shutdown already disposed is benign.
            try { Stopping.Cancel(false); } catch (ObjectDisposedException) { }

            if (transport != null) await transport.DisposeAsync().NoContext();

            if (pump != null) await Task.WhenAny(pump, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken)).NoContext();
        }

        void TrackPumpStarted() {
            var live = Interlocked.Increment(ref _livePumps);

            int observed;

            do {
                observed = Volatile.Read(ref _maxLivePumps);

                if (observed >= live) return;
            } while (Interlocked.CompareExchange(ref _maxLivePumps, live, observed) != observed);
        }
    }

    /// <summary>
    /// Defers by throwing when a precondition isn't met, so the message isn't acknowledged and gets
    /// redelivered after the resubscribe.
    /// </summary>
    sealed class DeferringHandler(Func<IMessageConsumeContext, bool> shouldDefer) : BaseEventHandler {
        int _handled;

        public int HandledCount => Volatile.Read(ref _handled);

        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            if (!shouldDefer(context)) return new(EventHandlingStatus.Success);

            Interlocked.Increment(ref _handled);

            throw new InvalidOperationException($"Precondition not met for {context.Stream}:{context.GlobalPosition}");
        }
    }

    /// <summary>
    /// Does I/O over the same connection the subscription reads from, so it fails the moment that
    /// connection dies — which is how one transport failure becomes a batch of simultaneous nacks.
    /// </summary>
    sealed class ConnectionBoundHandler(Func<int, bool> holdsUntilConnectionDies) : BaseEventHandler {
        readonly ConcurrentDictionary<int, TaskCompletionSource> _dead = [];

        int _inFlight;

        public int InFlight => Volatile.Read(ref _inFlight);

        public void KillConnection(int transport) => Killed(transport).TrySetResult();

        TaskCompletionSource Killed(int transport) => _dead.GetOrAdd(transport, _ => new(TaskCreationOptions.RunContinuationsAsynchronously));

        public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            var transport = context.Items.GetItem<int>(TransportKey);
            var killed    = Killed(transport).Task;

            if (killed.IsCompleted) throw Dead(transport);

            if (!holdsUntilConnectionDies(transport)) return EventHandlingStatus.Success;

            // Hold the operation open, so the failure catches the whole batch at once.
            Interlocked.Increment(ref _inFlight);

            try { await killed.WaitAsync(context.CancellationToken).NoContext(); } finally { Interlocked.Decrement(ref _inFlight); }

            throw Dead(transport);
        }

        static IOException Dead(int transport) => new($"The HTTP/2 server closed the connection (transport {transport})");
    }

    sealed class CountingHandler : BaseEventHandler {
        public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) => new(EventHandlingStatus.Success);
    }

    sealed class RecordingLoggerFactory : ILoggerFactory {
        readonly ConcurrentQueue<string> _messages = [];

        public int Count(string contains) => _messages.Count(m => m.Contains(contains));

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(_messages);

        public void AddProvider(ILoggerProvider provider) { }

        public void Dispose() { }

        sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => messages.Enqueue(formatter(state, exception));
        }
    }
}
