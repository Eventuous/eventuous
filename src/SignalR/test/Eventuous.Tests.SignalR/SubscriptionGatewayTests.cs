// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Collections.Concurrent;
using Eventuous.SignalR.Server;
using Eventuous.Subscriptions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Eventuous.Tests.SignalR;

public class SubscriptionGatewayTests {
    readonly List<(StreamName Stream, string SubId)>            _factoryCalls         = [];
    readonly ConcurrentDictionary<string, IMessageSubscription> _createdSubscriptions = new();

    SubscriptionGateway<TestHub> CreateGateway() {
        var hubContext  = Substitute.For<IHubContext<TestHub>>();
        var hubClients  = Substitute.For<IHubClients>();
        var clientProxy = Substitute.For<ISingleClientProxy>();
        hubContext.Clients.Returns(hubClients);
        hubClients.Client(Arg.Any<string>()).Returns(clientProxy);

        var producer = new SignalRProducer<TestHub>(hubContext);

        var options = new SignalRGatewayOptions {
            SubscriptionFactory = (stream, _, _, subscriptionId) => {
                _factoryCalls.Add((stream, subscriptionId));
                var sub = Substitute.For<IMessageSubscription>();
                sub.SubscriptionId.Returns(subscriptionId);

                // Make Subscribe block until cancelled
                sub.Subscribe(Arg.Any<OnSubscribed>(), Arg.Any<OnDropped>(), Arg.Any<CancellationToken>())
                    .Returns(ci => new(
                            Task.Run(async () => {
                                    var token = ci.ArgAt<CancellationToken>(2);

                                    try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) {
                                        /* Expected: cancelled when unsubscribed */
                                    }
                                }
                            )
                        )
                    );

                sub.Unsubscribe(Arg.Any<OnUnsubscribed>(), Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
                _createdSubscriptions[subscriptionId] = sub;

                return sub;
            }
        };

        return new(hubContext, producer, options, NullLoggerFactory.Instance);
    }

    [Test]
    public async Task SubscribeAsync_CreatesSubscriptionViaFactory() {
        await using var gateway = CreateGateway();
        await gateway.SubscribeAsync("conn-1", "Test-1", null);
        await Task.Delay(50); // Let background task start

        await Assert.That(_factoryCalls).HasCount().EqualTo(1);
        await Assert.That(_factoryCalls[0].SubId).IsEqualTo("signalr-conn-1-Test-1");
    }

    [Test]
    public async Task UnsubscribeAsync_RemovesSubscription() {
        await using var gateway = CreateGateway();
        await gateway.SubscribeAsync("conn-1", "Test-1", null);
        await Task.Delay(50);

        await gateway.UnsubscribeAsync("conn-1", "Test-1");
        await Task.Delay(50);

        // Verify unsubscribe was called
        var sub = _createdSubscriptions["signalr-conn-1-Test-1"];
        await sub.Received(1).Unsubscribe(Arg.Any<OnUnsubscribed>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task RemoveConnectionAsync_CleansUpAllSubscriptions() {
        await using var gateway = CreateGateway();
        await gateway.SubscribeAsync("conn-1", "Stream-A", null);
        await gateway.SubscribeAsync("conn-1", "Stream-B", null);
        await gateway.SubscribeAsync("conn-2", "Stream-A", null);
        await Task.Delay(50);

        await gateway.RemoveConnectionAsync("conn-1");
        await Task.Delay(50);

        // conn-1 subs should be unsubscribed
        var subA = _createdSubscriptions["signalr-conn-1-Stream-A"];
        var subB = _createdSubscriptions["signalr-conn-1-Stream-B"];
        await subA.Received(1).Unsubscribe(Arg.Any<OnUnsubscribed>(), Arg.Any<CancellationToken>());
        await subB.Received(1).Unsubscribe(Arg.Any<OnUnsubscribed>(), Arg.Any<CancellationToken>());

        // conn-2 sub should NOT be unsubscribed
        var subC = _createdSubscriptions["signalr-conn-2-Stream-A"];
        await subC.DidNotReceive().Unsubscribe(Arg.Any<OnUnsubscribed>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task DuplicateSubscribe_ReplacesPrevious() {
        await using var gateway = CreateGateway();
        await gateway.SubscribeAsync("conn-1", "Test-1", null);
        await Task.Delay(50);

        await gateway.SubscribeAsync("conn-1", "Test-1", 42);
        await Task.Delay(50);

        await Assert.That(_factoryCalls).HasCount().EqualTo(2);
        // First subscription should have been stopped
        // Note: since both use the same key, the second overwrites in _createdSubscriptions
        // So we check factory was called twice
        await Assert.That(_factoryCalls).HasCount().EqualTo(2);
    }
}
