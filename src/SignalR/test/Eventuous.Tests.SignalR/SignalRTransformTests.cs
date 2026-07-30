// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SignalR.Server;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.SignalR;

public class SignalRTransformTests {
    [Test]
    public async Task Transform_CreatesCorrectEnvelope() {
        TypeMap.RegisterKnownEventTypes(typeof(TransformTestEvent).Assembly);
        var serializer = EventSerializer.Default;
        var transform  = SignalRTransform.Create("conn-1", "Test-1", serializer);

        var ctx = new MessageConsumeContext(
            "aabbccdd-1234-5678-9012-aabbccddeeff",
            "TransformTestEvent",
            "application/json",
            "Test-1",
            0, 5, 42, 0,
            DateTime.UtcNow,
            new TransformTestEvent("hello"),
            null,
            "test-sub",
            CancellationToken.None
        );

        var result = await transform(ctx);

        await Assert.That(result).HasCount().EqualTo(1);

        var gm       = result[0];
        var envelope = (Eventuous.SignalR.StreamEventEnvelope)gm.Message;
        await Assert.That(envelope.Stream).IsEqualTo("Test-1");
        await Assert.That(envelope.EventType).IsEqualTo("TransformTestEvent");
        await Assert.That(envelope.StreamPosition).IsEqualTo(5UL);
        await Assert.That(envelope.GlobalPosition).IsEqualTo(42UL);
        await Assert.That(envelope.JsonPayload).IsNotNull();
        await Assert.That(envelope.JsonMetadata).IsNull();
        await Assert.That(gm.ProduceOptions.ConnectionId).IsEqualTo("conn-1");
    }

    [Test]
    public async Task Transform_IncludesMetadataWhenPresent() {
        TypeMap.RegisterKnownEventTypes(typeof(TransformTestEvent).Assembly);
        var serializer = EventSerializer.Default;
        var transform  = SignalRTransform.Create("conn-2", "Test-2", serializer);

        var meta = new Metadata { ["key1"] = "value1" };
        var ctx = new MessageConsumeContext(
            Guid.NewGuid().ToString(),
            "TransformTestEvent",
            "application/json",
            "Test-2",
            0, 10, 100, 0,
            DateTime.UtcNow,
            new TransformTestEvent("world"),
            meta,
            "test-sub",
            CancellationToken.None
        );

        var result   = await transform(ctx);
        var envelope = (Eventuous.SignalR.StreamEventEnvelope)result[0].Message;

        await Assert.That(envelope.JsonMetadata).IsNotNull();
    }
}

[EventType("TransformTestEvent")]
record TransformTestEvent(string Value);
