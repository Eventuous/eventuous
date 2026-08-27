// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapSkipTimeoutTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    const int SkipTimeoutMs = 2000;

    /// <summary>
    /// The skip timeout has to both hold the subscription for its configured duration and then let it past,
    /// so this asserts the hold before asserting the release: releasing immediately would satisfy the second
    /// assertion on its own.
    /// </summary>
    [Test]
    public async Task ShouldSkipGapOnlyAfterSkipTimeout(CancellationToken cancellationToken) {
        await Fixture.ArrangePermanentGap(new("test-stream-gap-skip"));

        await Fixture.StartSubscription();

        // Well inside the timeout, so the subscription is still holding at the gap
        await Task.Delay(SkipTimeoutMs / 4, cancellationToken);
        await Assert.That(Fixture.Handler.Handled.Count).IsEqualTo(2);

        var handled = await Fixture.WaitForHandled(5, TimeSpan.FromSeconds(10), cancellationToken);
        await Assert.That(handled).IsEqualTo(5);

        await Fixture.StopSubscription();

        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(0);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapSkipTimeoutMs     = SkipTimeoutMs;
        options.GapAgeThresholdMs    = null; // the gap must not be released by age
        options.GapHandlingTimeoutMs = null; // no tombstones
    }
}
