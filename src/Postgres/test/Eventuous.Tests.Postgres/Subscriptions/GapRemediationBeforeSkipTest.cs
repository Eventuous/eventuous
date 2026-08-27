// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapRemediationBeforeSkipTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    const int TimeoutMs = 2000;

    /// <summary>
    /// Both timeouts expire on the same poll, so remediation only runs if it takes precedence over the skip.
    /// The tombstone resolves the position safely — it conflicts with a committed row and blocks on an
    /// in-flight one — where skipping abandons it on elapsed time alone.
    /// </summary>
    [Test]
    public async Task ShouldCreateTombstoneBeforeSkippingGap(CancellationToken cancellationToken) {
        await Fixture.ArrangePermanentGap(new("test-stream-gap-remediation"));

        await Fixture.StartSubscription();

        // Well inside both timeouts: nothing has been remediated or skipped yet
        await Task.Delay(TimeoutMs / 4, cancellationToken);
        await Assert.That(Fixture.Handler.Handled.Count).IsEqualTo(2);
        await Assert.That(await Fixture.CountTombstones()).IsEqualTo(0);

        var handled = await Fixture.WaitForHandled(5, TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(handled).IsEqualTo(5);

        await Fixture.StopSubscription();

        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(1);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapHandlingTimeoutMs = TimeoutMs;
        options.GapSkipTimeoutMs     = TimeoutMs;
        options.GapAgeThresholdMs    = null; // the gap must not be released by age
    }
}
