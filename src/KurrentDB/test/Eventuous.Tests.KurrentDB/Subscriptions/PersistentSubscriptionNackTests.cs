// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tests.KurrentDB.Subscriptions.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using KurrentDB.Client;

namespace Eventuous.Tests.KurrentDB.Subscriptions;

/// <summary>
/// Verifies that persistent subscriptions send Nack to KurrentDB when ThrowOnError is enabled,
/// so that failed events are properly parked rather than silently lost. Regression test for #544.
/// </summary>
public class PersistentSubscriptionNackTests {
    [Test]
    [Category("Persistent subscription")]
    [Retry(3)]
    public async Task ShouldParkFailedEventWhenThrowOnErrorIsEnabled(CancellationToken cancellationToken) {
        string? connectionString = null;
        string? subscriptionId   = null;

        var handler = new NackTestHandler();

        var fixture = new PersistentSubscriptionFixture<
            StreamPersistentSubscription,
            StreamPersistentSubscriptionOptions,
            NackTestHandler>(
            handler,
            (id, connString, stream, h, loggerFactory) => {
                connectionString = connString;
                subscriptionId   = id;

                var settings = KurrentDBClientSettings.Create(connString);

                return new(
                    new KurrentDBClient(settings),
                    new() {
                        StreamName     = stream,
                        SubscriptionId = id,
                        ThrowOnError   = true,
                        FailureHandler = (_, subscription, resolvedEvent, exception)
                            => subscription.Nack(PersistentSubscriptionNakEventAction.Park, exception.Message, resolvedEvent)
                    },
                    new ConsumePipe().AddDefaultConsumer(h),
                    loggerFactory
                );
            },
            autoStart: false
        );

        await fixture.InitializeAsync();
        await fixture.Start();

        var testEvents = TestEvent.CreateMany(5);
        await fixture.Producer.Produce(fixture.Stream, testEvents, new(), cancellationToken: cancellationToken);

        // Wait for handler to fail once and then recover after resubscription
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(15));

        try {
            while (!cts.Token.IsCancellationRequested) {
                // After the first event is parked, the remaining 4 should be processed
                if (handler is { HasFailed: true, SuccessCount: >= 4 }) break;

                await Task.Delay(100, cts.Token);
            }
        } catch (OperationCanceledException) when (cts.Token.IsCancellationRequested) {
            // Fall through to assertions
        }

        await fixture.Stop();

        await Assert.That(handler.HasFailed).IsTrue();
        await Assert.That(handler.SuccessCount).IsGreaterThanOrEqualTo(4);

        // Verify that the failed event was actually parked in KurrentDB
        using var psClient = new KurrentDBPersistentSubscriptionsClient(KurrentDBClientSettings.Create(connectionString!));
        var info     = await psClient.GetInfoToStreamAsync(fixture.Stream, subscriptionId!, cancellationToken: cancellationToken);

        await Assert.That(info.Stats.ParkedMessageCount).IsGreaterThanOrEqualTo(1);

        await fixture.DisposeAsync();
    }
}

/// <summary>
/// Handler that throws on the first event it sees, then succeeds on all subsequent events.
/// Tracks both the failure and the number of successfully processed events.
/// </summary>
file class NackTestHandler : BaseEventHandler {
    int _failCount;
    int _successCount;

    public bool HasFailed    => Volatile.Read(ref _failCount) > 0;
    public int  SuccessCount => Volatile.Read(ref _successCount);

    public override ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        if (Interlocked.CompareExchange(ref _failCount, 1, 0) == 0) {
            throw new InvalidOperationException("Simulated handler failure for nack test");
        }

        Interlocked.Increment(ref _successCount);

        return new(EventHandlingStatus.Success);
    }
}
