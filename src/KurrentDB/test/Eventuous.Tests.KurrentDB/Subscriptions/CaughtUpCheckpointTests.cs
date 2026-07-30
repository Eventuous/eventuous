using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Producers;
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
/// Covers the caught-up commit for a filtered <see cref="AllStreamSubscription"/>: on a store smaller
/// than the server's checkpoint interval, no checkpoint message is ever sent during catch-up, so a
/// subscription whose filter matches nothing would otherwise never store any checkpoint at all — a
/// restart re-scans the whole log, and consumers comparing the checkpoint to the $all head see a
/// permanent phantom lag. The caught-up transition must commit the pre-subscribe $all head instead.
/// </summary>
public class CaughtUpCheckpointOnSmallStoreTests : StoreFixture {
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    IProducer           _producer        = null!;
    ICheckpointStore    _checkpointStore = null!;
    TestEventHandler    _handler         = null!;

    public CaughtUpCheckpointOnSmallStoreTests() : base(LogLevel.Information) {
        AutoStart = false;
        TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    [Test]
    [Category("Special cases")]
    [Timeout(60_000)]
    public async Task CheckpointCommittedOnCaughtUp(CancellationToken cancellationToken) {
        // Far fewer events than the checkpoint interval, so the server never sends a checkpoint
        // message during catch-up: only the caught-up transition can advance the checkpoint.
        const int count = 20;

        var testEvents = TestEvent.CreateMany(count);
        await _producer.Produce(_stream, testEvents, new(), cancellationToken: cancellationToken);

        var lastPosition = await GetLastAllStreamPosition(cancellationToken);

        await Start();

        var checkpoint = await PollUntilCheckpointReaches(lastPosition, TimeSpan.FromSeconds(30), cancellationToken);

        await DisposeAsync();

        // The filter never matched anything, so no event reached the handler...
        _handler.Count.ShouldBe(0);
        // ...yet catching up committed a checkpoint at or past the head observed before subscribing.
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
                        // A prefix that will never match the produced test events, and a checkpoint
                        // interval far larger than anything this test writes, so no server checkpoint
                        // message can mask a missing caught-up commit.
                        o.EventFilter        = EventTypeFilter.Prefix("definitely-does-not-match-anything");
                        o.CheckpointInterval = 4096;

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
/// Covers the caught-up commit composing with <see cref="SubscriptionOptions"/> StartFrom = Latest:
/// subscribing from the end of $all means the caught-up notification arrives immediately, and the
/// head observed before subscribing must be committed even though nothing is ever delivered.
/// </summary>
public class CaughtUpCheckpointFromLatestTests : StoreFixture {
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    IProducer           _producer        = null!;
    ICheckpointStore    _checkpointStore = null!;
    TestEventHandler    _handler         = null!;

    public CaughtUpCheckpointFromLatestTests() : base(LogLevel.Information) {
        AutoStart = false;
        TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    [Test]
    [Category("Special cases")]
    [Timeout(60_000)]
    public async Task CheckpointCommittedOnImmediateCaughtUp(CancellationToken cancellationToken) {
        const int count = 20;

        var testEvents = TestEvent.CreateMany(count);
        await _producer.Produce(_stream, testEvents, new(), cancellationToken: cancellationToken);

        var lastPosition = await GetLastAllStreamPosition(cancellationToken);

        await Start();

        var checkpoint = await PollUntilCheckpointReaches(lastPosition, TimeSpan.FromSeconds(30), cancellationToken);

        await DisposeAsync();

        // Starting from Latest, nothing written before the start is ever delivered...
        _handler.Count.ShouldBe(0);
        // ...yet the immediate caught-up transition committed the head observed before subscribing.
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
                        // Never-matching filter so live noise (stats, system events) can't advance the
                        // checkpoint through regular acks and mask a missing caught-up commit.
                        o.EventFilter        = EventTypeFilter.Prefix("definitely-does-not-match-anything");
                        o.CheckpointInterval = 4096;
                        o.StartFrom          = InitialPosition.Latest;

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
