using Eventuous.Sqlite.Subscriptions;
using Eventuous.Sut.App;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using static Eventuous.Sut.App.Commands;
using static Eventuous.Sut.Domain.BookingEvents;

namespace Eventuous.Tests.Sqlite.Subscriptions;

/// <summary>
/// The two properties every transport has to hold now that a drop stops the previous run before starting the
/// next: teardown runs on a connection that will be used again, and a transport whose resources are single-use
/// has to rebuild them in <c>Connect</c> rather than restart them.
/// </summary>
/// <remarks>
/// Sqlite shares <c>SqlSubscriptionBase</c> with Postgres and SQL Server, which is where the largest transport
/// change in this rewrite landed — but it is the only one of the three that needs no container and runs on every
/// target framework, so it is both the cheapest place to catch a regression and the only one that would catch a
/// framework-specific one. It cannot reuse <c>SubscriptionRestartBase</c>, which is bound to a Docker container.
/// </remarks>
[NotInParallel]
public class SubscriptionRestart() : SubscriptionTestBase(Fixture) {
    static readonly SubscriptionFixture<SqliteAllStreamSubscription, SqliteAllStreamSubscriptionOptions, TestEventHandler> Fixture
        = new(_ => { }, false);

    /// <summary>
    /// Unsubscribing twice must not throw. The framework only tears a live run down, but a provider can still be
    /// asked to release what it has already released — through an explicit stop, or a drop landing while
    /// shutdown is in flight.
    /// </summary>
    [Test]
    public async Task Sqlite_ShouldTolerateRepeatedUnsubscribe() {
        await Fixture.StartSubscription();
        await Fixture.StopSubscription();
        await Fixture.StopSubscription();
    }

    /// <summary>
    /// Subscribing again on the same instance after a full stop must consume newly produced events. Asserted by
    /// event identity, so a replay of the first batch cannot pass for the second — which is what a
    /// <c>Connect</c> that reused a spent reader would produce.
    /// </summary>
    [Test]
    public async Task Sqlite_ShouldConsumeAfterResubscribe(CancellationToken cancellationToken) {
        const int batch = 5;

        var started = false;

        try {
            var first = (await GenerateAndHandleCommands(batch)).Select(ToEvent).ToList();
            await Fixture.StartSubscription();
            started = true;
            await Assert.That(await WaitForEvents(first, cancellationToken)).IsTrue();

            await Fixture.StopSubscription();
            started = false;

            await Fixture.StartSubscription();
            started = true;

            // Produced after the restart, so nothing the first run consumed can pass for them.
            var second   = (await GenerateAndHandleCommands(batch)).Select(ToEvent).ToList();
            var consumed = await WaitForEvents(second, cancellationToken);

            await Fixture.StopSubscription();
            started = false;

            await Assert.That(consumed).IsTrue();
        } finally {
            if (started) {
                try { await Fixture.StopSubscription(); } catch (Exception) { /* cleanup only */ }
            }
        }
    }

    static async Task<bool> WaitForEvents(List<BookingImported> expected, CancellationToken cancellationToken) {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));

        try {
            while (true) {
                var handled = Fixture.Handler.Handled;

                if (expected.All(handled.Contains)) return true;

                await Task.Delay(200, cts.Token);
            }
        } catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    static BookingImported ToEvent(ImportBooking cmd) => new(cmd.RoomId, cmd.Price, cmd.CheckIn, cmd.CheckOut);

    static async Task<List<ImportBooking>> GenerateAndHandleCommands(int count) {
        var commands = Enumerable.Range(0, count).Select(_ => DomainFixture.CreateImportBooking()).ToList();
        var service  = new BookingService(Fixture.EventStore);

        foreach (var cmd in commands) {
            var result = await service.Handle(cmd, default);
            result.ThrowIfError();
        }

        return commands;
    }
}
