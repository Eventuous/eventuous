// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapRemediationBeforeSkipTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    [Test]
    public async Task ShouldCreateTombstoneBeforeSkippingGap(CancellationToken cancellationToken) {
        var streamName = new StreamName("test-stream-gap-remediation");

        await Fixture.AppendEvents(streamName, [.. Fixture.CreateEvents(2)], ExpectedStreamVersion.NoStream);

        await Fixture.InsertGap(streamName, 1);

        await Fixture.AppendEvents(streamName, [.. Fixture.CreateEvents(3)], ExpectedStreamVersion.Any);

        await Fixture.StartSubscription();

        await Fixture.Handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(10))
            .Exactly(5)
            .Match(_ => true)
            .Validate(cancellationToken);

        await Fixture.StopSubscription();

        // The tombstone resolves the gap safely: it can only be inserted once the position is proven dead,
        // so it must be attempted before the skip timeout is allowed to advance the subscription past it.
        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(1);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        // Both timeouts expire on the same poll, so remediation only runs if it takes precedence over the skip
        options.GapHandlingTimeoutMs = 500;
        options.GapSkipTimeoutMs     = 500;
        options.GapAgeThresholdMs    = null; // the gap must not be released by age
    }
}
