// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapSkipTimeoutTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    [Test]
    public async Task ShouldSkipGapAfterSkipTimeout(CancellationToken cancellationToken) {
        var streamName = new StreamName("test-stream-gap-skip");

        await Fixture.AppendEvents(streamName, [.. Fixture.CreateEvents(2)], ExpectedStreamVersion.NoStream);

        // The rolled back append burnt a global position that no transaction will ever fill,
        // so the gap can only be released by the skip timeout.
        await Fixture.InsertGap(streamName, 1);

        await Fixture.AppendEvents(streamName, [.. Fixture.CreateEvents(3)], ExpectedStreamVersion.Any);

        await Fixture.StartSubscription();

        await Fixture.Handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(10))
            .Exactly(5)
            .Match(_ => true)
            .Validate(cancellationToken);

        await Fixture.StopSubscription();

        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(0);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapSkipTimeoutMs     = 500;  // the gap must hold the subscription for this long, then be skipped
        options.GapAgeThresholdMs    = null; // never release a gap by age, so the skip timeout is the only way out
        options.GapHandlingTimeoutMs = null; // no tombstones
    }
}
