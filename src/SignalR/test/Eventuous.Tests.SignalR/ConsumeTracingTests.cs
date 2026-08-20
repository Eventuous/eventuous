// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

extern alias SignalRClient;
using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using SignalRClient::Eventuous.SignalR.Client;

namespace Eventuous.Tests.SignalR;

[NotInParallel]
public class ConsumeTracingTests : IDisposable {
    const string ProducerTraceId = "0af7651916cd43dd8448eb211c80319c";
    const string ProducerSpanId  = "b7ad6b7169203331";

    readonly ActivityListener _listener;

    public ConsumeTracingTests() {
        _listener = new() {
            ShouldListenTo = source => source.Name == EventuousDiagnostics.InstrumentationName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
        Activity.Current = null;
    }

    static string MetadataWithTracingContext()
        => JsonSerializer.Serialize(
            new Dictionary<string, object?> {
                [DiagnosticTags.TraceId] = ProducerTraceId,
                [DiagnosticTags.SpanId]  = ProducerSpanId
            }
        );

    [Test]
    public async Task ShouldLinkToRestoredContextInsteadOfParentingToIt() {
        using var activity = TypedStreamSubscription.StartTraceActivity(MetadataWithTracingContext());

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.Parent).IsNull();
        await Assert.That(activity.ParentSpanId.ToHexString()).IsEqualTo("0000000000000000");
        await Assert.That(activity.TraceId.ToHexString()).IsNotEqualTo(ProducerTraceId);

        var links = activity.Links.ToArray();
        await Assert.That(links.Length).IsEqualTo(1);
        await Assert.That(links[0].Context.TraceId.ToHexString()).IsEqualTo(ProducerTraceId);
        await Assert.That(links[0].Context.SpanId.ToHexString()).IsEqualTo(ProducerSpanId);
    }

    [Test]
    public async Task ShouldProduceDistinctTracesOnRedelivery() {
        using var first  = TypedStreamSubscription.StartTraceActivity(MetadataWithTracingContext());
        using var second = TypedStreamSubscription.StartTraceActivity(MetadataWithTracingContext());

        await Assert.That(first!.TraceId.ToHexString()).IsNotEqualTo(second!.TraceId.ToHexString());
    }

    [Test]
    public async Task ShouldReturnNullWithoutTracingContext() {
        using var activity = TypedStreamSubscription.StartTraceActivity("""{"some":"meta"}""");

        await Assert.That(activity).IsNull();
    }

    [Test]
    public async Task ShouldReturnNullOnMalformedMetadata() {
        using var activity = TypedStreamSubscription.StartTraceActivity("not json");

        await Assert.That(activity).IsNull();
    }

    public void Dispose() => _listener.Dispose();
}
