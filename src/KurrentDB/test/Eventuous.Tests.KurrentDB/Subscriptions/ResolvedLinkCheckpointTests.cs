using System.Text;
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
/// Covers the checkpoint position used for resolved link events: the subscription cursor is the
/// link's position in $all, not the resolved target's, which can be arbitrarily older. A link
/// created after the caught-up commit and pointing at an old event would otherwise flow the
/// target's stale position into the checkpoint on ack — the commit machinery is sequence-gated,
/// not position-monotonic, so the stored checkpoint would regress below the committed head and
/// never reach the link.
/// </summary>
public class ResolvedLinkCheckpointTests : StoreFixture {
    readonly string     _subscriptionId  = $"test-{Guid.NewGuid():N}";
    readonly StreamName _stream          = new($"test-{Guid.NewGuid():N}");
    readonly string     _linkStream      = $"link-{Guid.NewGuid():N}";
    IProducer           _producer        = null!;
    ICheckpointStore    _checkpointStore = null!;
    TestEventHandler    _handler         = null!;

    public ResolvedLinkCheckpointTests() : base(LogLevel.Information) {
        AutoStart = false;
        TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);
    }

    [Test]
    [Category("Special cases")]
    [Timeout(60_000)]
    public async Task CheckpointAdvancesToLinkPositionNotTargetPosition(CancellationToken cancellationToken) {
        await _producer.Produce(_stream, TestEvent.Create(), new(), cancellationToken: cancellationToken);

        var headBeforeStart = await GetLastAllStreamPosition(cancellationToken);

        await Start();

        // Precondition: the subscription caught up and committed at or past the pre-start head
        var caughtUp = await PollUntilCheckpointReaches(headBeforeStart, TimeSpan.FromSeconds(30), cancellationToken);
        caughtUp.Position.ShouldNotBeNull();
        caughtUp.Position!.Value.ShouldBeGreaterThanOrEqualTo(headBeforeStart);

        // A link written after the caught-up commit, resolving to the much older first event: its ack
        // must commit the link's own position, not drag the checkpoint back to the target's
        var linkResult = await Client.AppendToStreamAsync(
            _linkStream,
            StreamState.Any,
            [new(Uuid.NewUuid(), "$>", Encoding.UTF8.GetBytes($"0@{_stream}"), contentType: "application/octet-stream")],
            cancellationToken: cancellationToken
        );

        var linkPosition = linkResult.LogPosition.CommitPosition;

        var checkpoint = await PollUntilCheckpointReaches(linkPosition, TimeSpan.FromSeconds(30), cancellationToken);

        await DisposeAsync();

        // The event reached the handler directly and through the manual link at minimum — system
        // projections on a fresh server emit further $>-typed links ($ce-*, $et-*) resolving to the
        // same events, so the exact count is server-dependent...
        _handler.Count.ShouldBeGreaterThanOrEqualTo(2);
        // ...and the checkpoint advanced to the link's position instead of regressing to the target's.
        checkpoint.Position.ShouldNotBeNull();
        checkpoint.Position!.Value.ShouldBeGreaterThanOrEqualTo(linkPosition);
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
                        // Matches the produced test events and link records; the large checkpoint
                        // interval keeps server checkpoint messages from masking a checkpoint that
                        // moved through the wrong (target instead of link) position.
                        o.EventFilter        = EventTypeFilter.Prefix(TestEvent.TypeName, "$>");
                        o.CheckpointInterval = 4096;
                        o.ResolveLinkTos     = true;

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
