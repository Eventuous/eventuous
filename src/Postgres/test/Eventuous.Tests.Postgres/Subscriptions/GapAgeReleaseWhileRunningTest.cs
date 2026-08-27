// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapAgeReleaseWhileRunningTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    const int AgeThresholdMs = 2000;

    /// <summary>
    /// The age threshold has to release a gap that was first detected while the event following it was still
    /// young, not only one that was already old when the subscription started. With no skip timeout and no
    /// remediation configured — the defaults — it is the only thing that ever lets the subscription past a
    /// position no transaction will fill.
    /// </summary>
    [Test]
    public async Task ShouldReleaseGapThatAgesOutWhileSubscribed(CancellationToken cancellationToken) {
        await Fixture.ArrangePermanentGap(new("test-stream-gap-ages-out"));

        await Fixture.StartSubscription();

        // The event after the gap is younger than the threshold, so the subscription holds at the gap
        await Task.Delay(AgeThresholdMs / 4, cancellationToken);
        await Assert.That(Fixture.Handler.Handled.Count).IsEqualTo(2);

        // Once that event is older than GapAgeThresholdMs the gap is abandoned and the rest is handled
        var handled = await Fixture.WaitForHandled(5, TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(handled).IsEqualTo(5);

        await Fixture.StopSubscription();

        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(0);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapAgeThresholdMs    = AgeThresholdMs;
        options.GapSkipTimeoutMs     = null; // the default: never abandon a position on elapsed time alone
        options.GapHandlingTimeoutMs = null; // the default: no remediation
    }
}
