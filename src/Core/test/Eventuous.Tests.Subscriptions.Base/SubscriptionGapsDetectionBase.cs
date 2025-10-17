// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using DotNet.Testcontainers.Containers;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Tests.Persistence.Base.Fixtures;

namespace Eventuous.Tests.Subscriptions.Base;

public abstract class SubscriptionGapsDetectionBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore>(
        SubscriptionFixtureBase<TContainer, TSubscription, TSubscriptionOptions, TCheckpointStore, TestEventHandler> fixture
    ) : SubscriptionTestBase(fixture)
    where TContainer : DockerContainer
    where TSubscription : EventSubscription<TSubscriptionOptions>
    where TSubscriptionOptions : SubscriptionOptions
    where TCheckpointStore : class, ICheckpointStore {

    protected async Task ShouldNotSkipEvents(CancellationToken cancellationToken) {
        const int streamsCount    = 10;
        const int eventsPerStream = 10;

        await fixture.StartSubscription();
        await AppendEventsConcurrently();

        await fixture.Handler.AssertThat()
            .Timebox(TimeSpan.FromSeconds(2))
            .Exactly(streamsCount * eventsPerStream)
            .Match(_ => true)
            .Validate(cancellationToken);
        await fixture.StopSubscription();

        return;

        Task AppendEventsConcurrently() {
            var tasks = Enumerable.Range(0, streamsCount)
                .Select(async streamIndex => {
                        var streamName = new StreamName($"test-stream-{streamIndex}");
                        var events     = fixture.CreateEvents(eventsPerStream).Cast<object>().ToArray();

                        return await fixture.AppendEvents(streamName, events, ExpectedStreamVersion.NoStream);
                    }
                )
                .ToArray();

            return Task.WhenAll(tasks);
        }
    }
}
