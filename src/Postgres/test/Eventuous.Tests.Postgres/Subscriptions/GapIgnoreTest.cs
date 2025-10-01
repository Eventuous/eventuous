// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class GapIgnoreTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    [Test]
    public async Task ShouldIgnoreOldGaps(CancellationToken cancellationToken) {
        var streamName = new StreamName("test-stream-gap-ignore");

        await Fixture.AppendEvents(streamName, Fixture.CreateEvents(2).Cast<object>().ToArray(), ExpectedStreamVersion.NoStream);

        // Create a gap that will become "old"
        await Fixture.InsertGap(streamName, 1);

        await Fixture.AppendEvents(streamName, Fixture.CreateEvents(3).Cast<object>().ToArray(), ExpectedStreamVersion.Any);

        await Task.Delay(1000, cancellationToken);

        // Start subscription - should ignore the old gap and process available events
        await Fixture.StartSubscription();

        // Should process all 5 events despite the gap at position 2
        await Fixture.Handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(10))
            .Exactly(5)
            .Match(_ => true)
            .Validate(cancellationToken);

        await Fixture.StopSubscription();

        // Verify no tombstone were created since gap was ignored
        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(0);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapHandlingTimeoutMs = 0; // try to create tombstone right away
        options.GapAgeThresholdMs    = 500;
    }
}
