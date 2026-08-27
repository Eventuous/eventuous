using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Subscriptions.Base;

/// <summary>
/// Base test for the subscription end-of-stream measure used by the gap/lag diagnostics. Verifies that the
/// measure reports a valid position both on an empty store and after events are appended. Reproduces GitHub
/// #548, where the relational implementation queried the wrong column, threw on 32-bit position columns, and
/// failed on the <c>NULL</c> returned by <c>MAX(...)</c> over an empty table.
/// </summary>
public abstract class SubscriptionMeasureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    protected async Task ShouldMeasureEndOfStream(CancellationToken cancellationToken) {
        var measure = fixture.GetMeasure();

        // An empty store must yield a valid measure at position zero, not EndOfStream.Invalid.
        var empty = await measure(cancellationToken);
        await Assert.That(empty.SubscriptionId).IsEqualTo(fixture.SubscriptionId);
        await Assert.That(empty.Position).IsEqualTo(0ul);

        // After appending events, the measure must report the global end position.
        await GenerateAndHandleCommands(10);
        var last = await fixture.GetLastPosition();

        var measured = await measure(cancellationToken);
        await Assert.That(measured.SubscriptionId).IsEqualTo(fixture.SubscriptionId);
        await Assert.That(measured.Position).IsEqualTo(last);
    }

    /// <summary>
    /// For a stream subscription, the measure must report the tail of the subscribed stream only. Reproduces GitHub
    /// #586, where the relational implementation returned <c>MAX(stream_position)</c> across every stream in the
    /// store, so any longer stream inflated the gap of a fully caught-up subscription.
    /// </summary>
    protected async Task ShouldMeasureEndOfSubscribedStream(StreamName streamName, CancellationToken cancellationToken) {
        const int otherStreamLength      = 20;
        const int subscribedStreamLength = 5;

        var measure = fixture.GetMeasure();

        // Invoked before the subscription has ever connected, as the metrics observer does. The subscribed stream
        // doesn't exist yet, which must read as an empty stream rather than EndOfStream.Invalid.
        var empty = await measure(cancellationToken);
        await Assert.That(empty.SubscriptionId).IsEqualTo(fixture.SubscriptionId);
        await Assert.That(empty.Position).IsEqualTo(0ul);

        // A longer, unrelated stream: its tail must not leak into the subscribed stream's measure.
        await fixture.AppendEvents(new($"other-{Guid.NewGuid():N}"), [.. fixture.CreateEvents(otherStreamLength)], ExpectedStreamVersion.NoStream);

        var events = fixture.CreateEvents(subscribedStreamLength).ToList();
        await fixture.AppendEvents(streamName, [.. events], ExpectedStreamVersion.NoStream);

        var measured = await measure(cancellationToken);
        await Assert.That(measured.SubscriptionId).IsEqualTo(fixture.SubscriptionId);
        await Assert.That(measured.Position).IsEqualTo((ulong)(subscribedStreamLength - 1));

        await fixture.StartSubscription();
        await fixture.Handler.AssertCollection(TimeSpan.FromSeconds(2), [.. events]).Validate(cancellationToken);
        await fixture.StopSubscription();

        // Once caught up, the gap the metrics derive from this measure and the checkpoint must be zero.
        var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);
        var caughtUp   = await measure(cancellationToken);
        await Assert.That(caughtUp.Position - checkpoint.Position!.Value).IsEqualTo(0ul);
    }

    async Task GenerateAndHandleCommands(int count) {
        var commands = Enumerable
            .Range(0, count)
            .Select(_ => DomainFixture.CreateImportBooking())
            .ToList();

        var service = new BookingService(fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }
    }
}
