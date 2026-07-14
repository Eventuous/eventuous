using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Registrations;
using Eventuous.TestHelpers.TUnit;
using Eventuous.Tests.Subscriptions.Base;
using KurrentDB.Client;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

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
