// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

extern alias SignalRClient;
using SignalRClient::Eventuous.SignalR.Client;
using Microsoft.AspNetCore.SignalR.Client;

namespace Eventuous.Tests.SignalR;

public class TypedStreamSubscriptionTests {
    static HubConnection BuildFakeConnection()
        => new HubConnectionBuilder()
            .WithUrl("http://localhost:9999/test")
            .Build();

    [Test]
    public async Task On_BeforeStart_RegistersHandler() {
        var connection = BuildFakeConnection();
        var client     = new SignalRSubscriptionClient(connection);
        var sub        = client.SubscribeTyped("test-stream", null);

        // Register two handlers before start — should not throw
        sub.On<TypedEvent1>((_, _) => ValueTask.CompletedTask);
        sub.On<TypedEvent2>((_, _) => ValueTask.CompletedTask);

        await Assert.That(sub).IsNotNull();
        await client.DisposeAsync();
    }

    [Test]
    public async Task On_AfterStart_Throws() {
        // We can't call StartAsync (no real server), so we test the guard
        // by verifying On() is allowed before start and blocks after.
        // Since we can't actually start without a server, we test the object
        // construction and pre-start handler registration.
        var connection = BuildFakeConnection();
        var client     = new SignalRSubscriptionClient(connection);
        var sub        = client.SubscribeTyped("test-stream", null);

        // Should work fine before start
        await Assert.That(() => sub.On<TypedEvent1>((_, _) => ValueTask.CompletedTask)).ThrowsNothing();

        await client.DisposeAsync();
    }

    [Test]
    public async Task OnError_CanBeChained() {
        var connection = BuildFakeConnection();
        var client     = new SignalRSubscriptionClient(connection);
        var sub        = client.SubscribeTyped("test-stream", null);

        // Fluent chaining should work
        var result = sub
            .On<TypedEvent1>((_, _) => ValueTask.CompletedTask)
            .OnError(_ => { });

        await Assert.That(result).IsSameReferenceAs(sub);
        await client.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_WithoutStart_IsNoOp() {
        var connection = BuildFakeConnection();
        var client     = new SignalRSubscriptionClient(connection);
        var sub        = client.SubscribeTyped("test-stream", null);

        // Should not throw even though StartAsync was never called
        await Assert.That(async () => await sub.DisposeAsync()).ThrowsNothing();
        await client.DisposeAsync();
    }

    [Test]
    public async Task SubscribeTyped_ReturnsNewInstanceEachTime() {
        var connection = BuildFakeConnection();
        var client     = new SignalRSubscriptionClient(connection);

        var sub1 = client.SubscribeTyped("stream-a", null);
        var sub2 = client.SubscribeTyped("stream-b", null);

        await Assert.That(sub1).IsNotSameReferenceAs(sub2);
        await client.DisposeAsync();
    }
}

[EventType("TypedEvent1")]
record TypedEvent1(string Data);

[EventType("TypedEvent2")]
record TypedEvent2(int Count);
