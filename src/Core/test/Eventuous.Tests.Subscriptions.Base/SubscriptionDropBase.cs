using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eventuous.Tests.Subscriptions.Base;

/// <summary>
/// Base test that verifies a subscription drops and resubscribes when the underlying infrastructure
/// connection is lost and later restored, and that the subscription health check reports
/// <see cref="HealthStatus.Unhealthy"/> while dropped and <see cref="HealthStatus.Healthy"/> again after
/// recovery. The connection loss is simulated by pausing the infrastructure container (Docker pause), which
/// freezes the database process while keeping the published port intact, so the same connection string keeps
/// working once the container is unpaused. Reproduces the scenario from GitHub #308 (and #307).
/// </summary>
public abstract class SubscriptionDropBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    const int BatchSize = 5;

    // The poll/connection failure that follows a pause surfaces only after the provider's command/connection
    // timeout elapses, so give detection (and recovery) a generous window.
    static readonly TimeSpan DropTimeout = TimeSpan.FromSeconds(90);

    /// <summary>
    /// Produces and consumes events, drops the connection by pausing the container, asserts the subscription is
    /// reported unhealthy, restores the connection, then asserts that the subscription resubscribes, reports
    /// healthy again, and resumes processing newly produced events.
    /// </summary>
    protected async Task ShouldResubscribeAfterConnectionDrop(CancellationToken cancellationToken) {
        var subscriptionStarted = false;
        var containerPaused     = false;

        try {
            // 1. Produce and consume an initial batch; the subscription must be healthy.
            await GenerateAndHandleCommands(BatchSize);
            await fixture.StartSubscription();
            subscriptionStarted = true;
            var consumedInitial = await WaitUntil(() => fixture.Handler.Count >= BatchSize, DropTimeout, cancellationToken);
            await Assert.That(consumedInitial).IsTrue();
            await Assert.That(await GetHealthStatus(cancellationToken)).IsEqualTo(HealthStatus.Healthy);

            // Wait until the initial batch is committed to the checkpoint. Otherwise the subscription
            // replays those uncommitted events on resubscribe, and the replay could satisfy the
            // post-recovery assertion below without any newly produced event ever being processed.
            var lastPosition = await fixture.GetLastPosition();
            var checkpointed = await WaitUntil(
                async () => {
                    var checkpoint = await fixture.CheckpointStore.GetLastCheckpoint(fixture.SubscriptionId, cancellationToken);

                    return checkpoint.Position >= lastPosition;
                },
                DropTimeout,
                cancellationToken
            );
            await Assert.That(checkpointed).IsTrue();

            // 2. Drop the connection by pausing the container.
            WriteLine("Pausing the container to drop the connection");
            await fixture.Container.PauseAsync(cancellationToken);
            containerPaused = true;

            // 3. The subscription must detect the drop and report unhealthy.
            var dropped = await WaitUntil(() => fixture.IsDropped, DropTimeout, cancellationToken);
            await Assert.That(dropped).IsTrue();
            var unhealthy = await WaitUntil(async () => await GetHealthStatus(cancellationToken) == HealthStatus.Unhealthy, DropTimeout, cancellationToken);
            await Assert.That(unhealthy).IsTrue();
            WriteLine("Subscription dropped and reported unhealthy");

            // 4. Restore the connection.
            WriteLine("Unpausing the container to restore the connection");
            await fixture.Container.UnpauseAsync(cancellationToken);
            containerPaused = false;

            // 5. The subscription must resubscribe and report healthy again.
            var recovered = await WaitUntil(() => !fixture.IsDropped, DropTimeout, cancellationToken);
            await Assert.That(recovered).IsTrue();
            var healthy = await WaitUntil(async () => await GetHealthStatus(cancellationToken) == HealthStatus.Healthy, DropTimeout, cancellationToken);
            await Assert.That(healthy).IsTrue();
            WriteLine("Subscription resubscribed and reported healthy");

            // 6. Events produced after recovery must be processed. The initial batch is already
            // checkpointed, so it cannot be replayed and this count only advances for new events.
            var countBeforeRecovery = fixture.Handler.Count;
            await GenerateAndHandleCommands(BatchSize);
            var resumed = await WaitUntil(() => fixture.Handler.Count >= countBeforeRecovery + BatchSize, DropTimeout, cancellationToken);

            await fixture.StopSubscription();
            subscriptionStarted = false;

            await Assert.That(resumed).IsTrue();
            WriteLine("Processed {0} events after recovery", fixture.Handler.Count - countBeforeRecovery);
        } finally {
            // Undo the destructive steps even if an assertion fails or the test is cancelled mid-flight:
            // a container left paused, or a subscription left resubscribing, would contaminate later tests.
            // These fixtures use autoStart=false, so fixture disposal won't stop the subscription either.
            if (containerPaused) {
                try {
                    await fixture.Container.UnpauseAsync(CancellationToken.None);
                } catch (Exception ex) {
                    WriteLine("Cleanup: failed to unpause the container: {0}", ex.Message);
                }
            }

            if (subscriptionStarted) {
                try {
                    await fixture.StopSubscription();
                } catch (Exception ex) {
                    WriteLine("Cleanup: failed to stop the subscription: {0}", ex.Message);
                }
            }
        }
    }

    async Task<HealthStatus> GetHealthStatus(CancellationToken cancellationToken) {
        var result = await fixture.Health.CheckHealthAsync(new(), cancellationToken);

        return result.Status;
    }

    static Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout, CancellationToken cancellationToken)
        => WaitUntil(() => Task.FromResult(condition()), timeout, cancellationToken);

    static async Task<bool> WaitUntil(Func<Task<bool>> condition, TimeSpan timeout, CancellationToken cancellationToken) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeout);

        try {
            while (!await condition()) {
                await Task.Delay(200, cts.Token);
            }

            return true;
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) {
            return false;
        }
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
