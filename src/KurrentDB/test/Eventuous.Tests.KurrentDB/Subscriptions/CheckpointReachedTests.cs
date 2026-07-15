using System.Diagnostics;
using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Registrations;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.Subscriptions.Base;
using KurrentDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using EventTypeFilter = KurrentDB.Client.EventTypeFilter;

// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.KurrentDB.Subscriptions;

/// <summary>
/// Covers the fix for the checkpoint of a filtered <see cref="AllStreamSubscription"/> parking at the
/// last matched event: with a server-side event filter that never matches, the server-reported
/// checkpoint position must still flow into the checkpoint store, so a restart doesn't re-scan
/// everything since the last match.
/// </summary>
public class CheckpointReachedTests : StoreFixture {
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    IProducer           _producer        = null!;
    ICheckpointStore    _checkpointStore = null!;
    TestEventHandler    _handler         = null!;

    public CheckpointReachedTests() : base(LogLevel.Information) {
        AutoStart = false;
        TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    [Test]
    [Category("Special cases")]
    [Timeout(60_000)]
    public async Task CheckpointAdvancesPastUnmatchedEvents(CancellationToken cancellationToken) {
        // Enough unmatched events, and a small MaxSearchWindow, so the server reports a
        // checkpoint position well before it would have scanned the whole write.
        const int count = 500;

        var testEvents = TestEvent.CreateMany(count);
        await _producer.Produce(_stream, testEvents, new(), cancellationToken: cancellationToken);

        await Start();

        var lastPosition = await GetLastAllStreamPosition(cancellationToken);

        var checkpoint = await PollUntilCheckpointReaches(lastPosition, TimeSpan.FromSeconds(30), cancellationToken);

        await DisposeAsync();

        // The filter never matched anything, so no event reached the handler...
        _handler.Count.ShouldBe(0);
        // ...yet the checkpoint advanced past the last written (unmatched) event.
        checkpoint.Position.ShouldNotBeNull();
        checkpoint.Position!.Value.ShouldBeGreaterThanOrEqualTo(lastPosition);
    }

    async Task<ulong> GetLastAllStreamPosition(CancellationToken cancellationToken) {
        var lastEvent = await Client.ReadAllAsync(Direction.Backwards, Position.End, 1, cancellationToken: cancellationToken).ToArrayAsync(cancellationToken);

        return lastEvent.Length == 0 ? 0 : lastEvent[0].Event.Position.CommitPosition;
    }

    // Polls until the checkpoint reaches minPosition, or returns the last-seen checkpoint once the
    // deadline passes (the assertions in the test produce a clear failure message in that case).
    async Task<Checkpoint> PollUntilCheckpointReaches(ulong minPosition, TimeSpan timeout, CancellationToken cancellationToken) {
        var deadline   = DateTime.UtcNow + timeout;
        var checkpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);

        while (!(checkpoint.Position is { } position && position >= minPosition) && DateTime.UtcNow < deadline) {
            await Task.Delay(200.Milliseconds(), cancellationToken);
            checkpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);
        }

        return checkpoint;
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddProducer<KurrentDBProducer>();

        services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
            _subscriptionId,
            c => c
                .Configure(
                    o => {
                        // A prefix that will never match the produced test events, so the filter
                        // excludes everything, but with a small enough search window that the
                        // server still reports checkpoint progress while scanning past them.
                        o.EventFilter        = EventTypeFilter.Prefix(4, "definitely-does-not-match-anything");
                        o.CheckpointInterval = 1;

                        o.CheckpointCommitBatchSize = 1;
                        o.CheckpointCommitDelayMs   = 100;
                    }
                )
                .UseCheckpointStore<TestCheckpointStore>()
                .AddEventHandler<TestEventHandler>()
        );
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        _producer        = provider.GetRequiredService<IProducer>();
        _checkpointStore = provider.GetRequiredKeyedService<TestCheckpointStore>(_subscriptionId);
        _handler         = provider.GetRequiredKeyedService<TestEventHandler>(_subscriptionId);
    }
}

/// <summary>
/// Covers the safety half of the checkpointReached wiring: a synthetic checkpoint marker for an
/// unmatched event must never let the stored checkpoint pass a filter-matched event whose ack is
/// still pending. <see cref="CheckpointCommitHandler"/>'s contiguous-sequence gate means the matched
/// event — produced (and therefore delivered) before any of the unmatched noise this test writes —
/// blocks every later position from that noise, including checkpoint markers, from committing past it
/// until it is acknowledged. If checkpoint markers were ever committed directly instead of flowing
/// through that same gated commit path, this test would observe the checkpoint racing past the
/// matched event's position while its ack is still held open.
/// </summary>
public class CheckpointGatedByPendingMatchTests : StoreFixture {
    readonly string        _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName    _stream          = new($"test-{Guid.NewGuid():N}");
    IProducer              _producer        = null!;
    ICheckpointStore       _checkpointStore = null!;
    BlockingEventHandler   _handler         = null!;

    public CheckpointGatedByPendingMatchTests() : base(LogLevel.Information) {
        AutoStart = false;
        TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly, typeof(UnmatchedEvent).Assembly);
    }

    [Test]
    [Category("Special cases")]
    [Timeout(60_000)]
    public async Task CheckpointDoesNotPassUnacknowledgedMatchedEvent(CancellationToken cancellationToken) {
        // Enough unmatched events, and a small MaxSearchWindow, so the server reports plenty of
        // checkpoint marker positions above the held-open matched event while we hold it.
        const int unmatchedCount = 500;

        // The matched event goes in first, so it gets the lowest sequence among everything our
        // filter can see: everything the subscription observes afterwards (real or synthetic) must
        // wait behind its ack. (A fresh KurrentDB instance can carry a little bit of unrelated,
        // unmatched system/metadata activity before this point — that's fine, and legitimately
        // committable on its own, since none of it depends on our event's ack.)
        await _producer.Produce(_stream, TestEvent.Create(), new(), cancellationToken: cancellationToken);
        var matchedEventPosition = await GetLastAllStreamPosition(cancellationToken);

        await _producer.Produce(_stream, UnmatchedEvent.CreateMany(unmatchedCount), new(), cancellationToken: cancellationToken);

        var lastPosition = await GetLastAllStreamPosition(cancellationToken);

        // Observes CheckpointCommitHandler's "Commit" diagnostic, which fires on every Commit call
        // BEFORE the contiguous-sequence gate: it proves positions reached the commit path without
        // saying anything about whether they were allowed to commit.
        using var commitObserver = new CommitDiagnosticCounter(_subscriptionId);

        await Start();

        // The handler must Release() no matter how the held-open section ends, otherwise a failed
        // assertion leaves the handler blocked and fixture disposal hanging until the test timeout.
        try {
            var started = await Task.WhenAny(_handler.Started, Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
            started.ShouldBe(_handler.Started, "The handler should have started processing the matched event before the hold-open assertion");

            // While the handler is blocked, the matched event's own ack cannot have fired, so every
            // observed Commit write is a checkpoint marker. Waiting for a marker whose position is
            // strictly beyond the matched event proves the dangerous kind of marker — one that would
            // overtake the pending ack if committed directly — actually entered the commit path
            // during the hold. A bare count wouldn't: a fresh KurrentDB carries some unmatched
            // system/metadata activity below the matched event, whose markers can be observed (and
            // legitimately committed) without ever exercising the gate.
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);

            while (!(commitObserver.MaxPosition is { } max && max > matchedEventPosition) && DateTime.UtcNow < deadline) {
                await Task.Delay(100, cancellationToken);
            }

            (commitObserver.MaxPosition ?? 0).ShouldBeGreaterThan(
                matchedEventPosition,
                "A checkpoint marker beyond the blocked matched event should reach the commit path while its ack is held open — none arriving means the checkpointReached wiring is broken"
            );

            // A past-the-event marker provably reached the commit path; the sequence gate must still
            // have held it (and every other position) behind the unacknowledged matched event.
            var stalledCheckpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);

            if (stalledCheckpoint.Position is { } stalledPosition) {
                stalledPosition.ShouldBeLessThan(
                    matchedEventPosition,
                    "No position at or past the matched (still unacknowledged) event should be committed"
                );
            }
        } finally {
            _handler.Release();
        }

        var checkpoint = await PollUntilCheckpointReaches(lastPosition, TimeSpan.FromSeconds(30), cancellationToken);

        await DisposeAsync();

        // Only the matched event ever reached the handler...
        _handler.Count.ShouldBe(1);
        // ...but once it was acknowledged, the checkpoint caught up past all the unmatched events.
        checkpoint.Position.ShouldNotBeNull();
        checkpoint.Position!.Value.ShouldBeGreaterThanOrEqualTo(lastPosition);
    }

    async Task<ulong> GetLastAllStreamPosition(CancellationToken cancellationToken) {
        var lastEvent = await Client.ReadAllAsync(Direction.Backwards, Position.End, 1, cancellationToken: cancellationToken).ToArrayAsync(cancellationToken);

        return lastEvent.Length == 0 ? 0 : lastEvent[0].Event.Position.CommitPosition;
    }

    // Polls until the checkpoint reaches minPosition, or returns the last-seen checkpoint once the
    // deadline passes (the assertions in the test produce a clear failure message in that case).
    async Task<Checkpoint> PollUntilCheckpointReaches(ulong minPosition, TimeSpan timeout, CancellationToken cancellationToken) {
        var deadline   = DateTime.UtcNow + timeout;
        var checkpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);

        while (!(checkpoint.Position is { } position && position >= minPosition) && DateTime.UtcNow < deadline) {
            await Task.Delay(200.Milliseconds(), cancellationToken);
            checkpoint = await _checkpointStore.GetLastCheckpoint(_subscriptionId, cancellationToken);
        }

        return checkpoint;
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddProducer<KurrentDBProducer>();

        services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
            _subscriptionId,
            c => c
                .Configure(
                    o => {
                        // A prefix that matches only the produced TestEvent instances, with a small
                        // enough search window that the server still reports frequent checkpoint
                        // progress while scanning past the unmatched noise.
                        o.EventFilter        = EventTypeFilter.Prefix(4, TestEvent.TypeName);
                        o.CheckpointInterval = 1;

                        o.CheckpointCommitBatchSize = 1;
                        o.CheckpointCommitDelayMs   = 100;
                    }
                )
                .UseCheckpointStore<TestCheckpointStore>()
                .AddEventHandler<BlockingEventHandler>()
        );
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        _producer        = provider.GetRequiredService<IProducer>();
        _checkpointStore = provider.GetRequiredKeyedService<TestCheckpointStore>(_subscriptionId);
        _handler         = provider.GetRequiredKeyedService<BlockingEventHandler>(_subscriptionId);
    }
}

/// <summary>
/// A second, distinct event type whose type name doesn't match the "test-event" prefix, used to
/// generate noise that the subscription's server-side filter excludes.
/// </summary>
[EventType(TypeName)]
// ReSharper disable once ClassNeverInstantiated.Global
public record UnmatchedEvent(int Number) {
    public const string TypeName = "unmatched-event";

    public static List<UnmatchedEvent> CreateMany(int count) => Enumerable.Range(0, count).Select(i => new UnmatchedEvent(i)).ToList();
}

/// <summary>
/// Like <see cref="TestEventHandler"/>, but holds the first event's ack open until <see cref="Release"/>
/// is called, so a test can observe checkpoint behaviour while a matched event's ack is still pending.
/// </summary>
public class BlockingEventHandler : BaseEventHandler {
    readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public int  Count   { get; private set; }
    public Task Started => _started.Task;

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        _started.TrySetResult();
        await _release.Task.WaitAsync(context.CancellationToken);
        Count++;

        return EventHandlingStatus.Success;
    }

    public void Release() => _release.TrySetResult();
}

/// <summary>
/// Observes <see cref="CheckpointCommitHandler"/> "Commit" diagnostic writes for one subscription,
/// tracking the highest commit position seen. The diagnostic fires on every <c>Commit</c> call
/// before the contiguous-sequence gate, so it observes positions reaching the commit path
/// regardless of whether they're allowed to commit. The payload (an internal <c>CommitEvent</c>)
/// carries the subscription id — read via reflection to keep parallel tests' commit handlers from
/// polluting the observation — and the <c>CommitPosition</c>, whose <c>Position</c> is what gets
/// tracked; if either property is renamed, nothing matches and the test's marker-arrival poll fails
/// loudly rather than silently passing.
/// </summary>
sealed class CommitDiagnosticCounter : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable {
    readonly string            _subscriptionId;
    readonly IDisposable       _allListeners;
    readonly List<IDisposable> _subscriptions = [];
    readonly object            _positionLock  = new();

    ulong? _maxPosition;

    public CommitDiagnosticCounter(string subscriptionId) {
        _subscriptionId = subscriptionId;
        _allListeners   = DiagnosticListener.AllListeners.Subscribe(this);
    }

    public ulong? MaxPosition {
        get {
            lock (_positionLock) return _maxPosition;
        }
    }

    // The commit handler's write is guarded by Diagnostic.IsEnabled(CommitOperation), so the
    // subscription must carry an IsEnabled predicate that answers true.
    void IObserver<DiagnosticListener>.OnNext(DiagnosticListener listener) {
        if (listener.Name != CheckpointCommitHandler.DiagnosticName) return;

        lock (_subscriptions) _subscriptions.Add(listener.Subscribe(this, (_, _, _) => true));
    }

    void IObserver<KeyValuePair<string, object?>>.OnNext(KeyValuePair<string, object?> evt) {
        if (evt.Key != CheckpointCommitHandler.CommitOperation) return;

        var payload = evt.Value;

        if (payload?.GetType().GetProperty("Id")?.GetValue(payload) is not string id || id != _subscriptionId) return;

        if (payload.GetType().GetProperty("CommitPosition")?.GetValue(payload) is not { } commitPosition) return;

        if (commitPosition.GetType().GetProperty("Position")?.GetValue(commitPosition) is not ulong position) return;

        lock (_positionLock) {
            if (_maxPosition is not { } max || position > max) _maxPosition = position;
        }
    }

    void IObserver<DiagnosticListener>.OnCompleted() { }

    void IObserver<DiagnosticListener>.OnError(Exception error) { }

    void IObserver<KeyValuePair<string, object?>>.OnCompleted() { }

    void IObserver<KeyValuePair<string, object?>>.OnError(Exception error) { }

    public void Dispose() {
        _allListeners.Dispose();

        lock (_subscriptions) {
            foreach (var subscription in _subscriptions) subscription.Dispose();

            _subscriptions.Clear();
        }
    }
}
