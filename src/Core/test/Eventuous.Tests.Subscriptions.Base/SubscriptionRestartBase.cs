using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Subscriptions.Base;

/// <summary>
/// The two properties the resubscribe path relies on from every transport. Since a drop now stops the
/// previous run before starting the next, teardown runs on a connection that will be used again, and a
/// transport whose resources are single-use has to rebuild them rather than restart them. Stated here so
/// each provider suite can assert it against real infrastructure.
/// </summary>
public abstract class SubscriptionRestartBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {
    const int BatchSize = 5;

    static readonly TimeSpan ConsumeTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Unsubscribing twice must not throw. The framework only calls transport teardown for a live run, but
    /// a provider may still be asked to release resources it has already released — through an explicit
    /// stop, or by a drop landing while shutdown is in flight.
    /// </summary>
    protected async Task ShouldTolerateRepeatedUnsubscribe() {
        await fixture.StartSubscription();
        await fixture.StopSubscription();
        await fixture.StopSubscription();
    }

    /// <summary>
    /// Subscribing again after a full stop must consume newly produced events. Asserts by event identity,
    /// so a replay of the first batch can't pass for the second.
    /// </summary>
    protected async Task ShouldConsumeAfterResubscribe(CancellationToken cancellationToken) {
        var started = false;

        try {
            var first = (await GenerateAndHandleCommands(BatchSize)).Select(ToEvent).ToList();
            await fixture.StartSubscription();
            started = true;
            await Assert.That(await WaitForEvents(first, cancellationToken)).IsTrue();

            await fixture.StopSubscription();
            started = false;
            WriteLine("Subscription stopped, starting it again on the same instance");

            await fixture.StartSubscription();
            started = true;

            var second   = (await GenerateAndHandleCommands(BatchSize)).Select(ToEvent).ToList();
            var consumed = await WaitForEvents(second, cancellationToken);

            await fixture.StopSubscription();
            started = false;

            await Assert.That(consumed).IsTrue();
        } finally {
            if (started) {
                try {
                    await fixture.StopSubscription();
                } catch (Exception ex) { WriteLine("Cleanup: failed to stop the subscription: {0}", ex.Message); }
            }
        }
    }

    async Task<bool> WaitForEvents(List<BookingImported> expected, CancellationToken cancellationToken) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(ConsumeTimeout);

        try {
            while (true) {
                var handled = fixture.Handler.Handled;
                if (expected.All(handled.Contains)) return true;

                await Task.Delay(200, cts.Token);
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable.Range(0, count).Select(_ => DomainFixture.CreateImportBooking()).ToList();
        var service  = new BookingService(fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }

        return commands;
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);
}
