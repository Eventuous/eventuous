// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Subscriptions;
using Eventuous.Tests.Persistence.Base.Fixtures;
using Eventuous.Tests.Subscriptions.Base;

namespace Eventuous.Tests.Postgres.Subscriptions;

[NotInParallel]
public class TombstonesCreationTest() : SubscriptionTestBase(Fixture) {
    static readonly TombstonesFixture Fixture = new(ConfigureOptions);

    [Test]
    public async Task ShouldCreateTombstones(CancellationToken cancellationToken) {
        var streamName = new StreamName($"test-stream");

        // create 2 events
        await Fixture.AppendEvents(streamName, Fixture.CreateEvents(2).Cast<object>().ToArray(), ExpectedStreamVersion.NoStream);

        // create gap
        await Fixture.InsertGap(streamName, 1);

        // create other 2 events
        await Fixture.AppendEvents(streamName, Fixture.CreateEvents(2).Cast<object>().ToArray(), ExpectedStreamVersion.Any);

        // create gaps
        await Fixture.InsertGap(streamName, 3);
        await Fixture.InsertGap(streamName, 3);

        // create other 2 events
        await Fixture.AppendEvents(streamName, Fixture.CreateEvents(2).Cast<object>().ToArray(), ExpectedStreamVersion.Any);

        await Fixture.StartSubscription();

        await Fixture.Handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(5))
            .Exactly(6)
            .Match(_ => true)
            .Validate(cancellationToken);
        await Fixture.StopSubscription();

        var tombstonesCount = await Fixture.CountTombstones();
        await Assert.That(tombstonesCount).IsEqualTo(3);
    }

    static void ConfigureOptions(PostgresAllStreamSubscriptionOptions options) {
        options.GapHandlingTimeoutMs = 500;
    }
}
