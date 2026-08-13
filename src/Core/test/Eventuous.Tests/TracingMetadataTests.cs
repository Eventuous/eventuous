// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using Eventuous.Diagnostics;
using Eventuous.TestHelpers;

namespace Eventuous.Tests;

[NotInParallel]
public class TracingMetadataTests : IDisposable {
    const string SourceName = "tracing.metadata.tests";

    readonly ActivitySource   _source = new(SourceName);
    readonly ActivityListener _listener;

    public TracingMetadataTests() {
        // The sampler does not read options.TraceId on purpose: .NET only materialises the trace id of a new root
        // when a sampler asks for it, so this is what an activity looks like before it is started.
        _listener = new() {
            ShouldListenTo = source => source.Name == SourceName,
            Sample         = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(_listener);
        Activity.Current = null;
    }

    [Test]
    public async Task ShouldPersistTracingMetaFromStartedActivity() {
        using var activity = _source.StartActivity("append");

        var (traceId, spanId) = new Metadata().AddActivityTags(activity).GetTracingMeta();

        await Assert.That(traceId).IsEqualTo(activity!.TraceId.ToHexString());
        await Assert.That(spanId).IsEqualTo(activity.SpanId.ToHexString());
    }

    [Test]
    public async Task ShouldNotPersistTracingMetaWithoutRealIds() {
        // An unstarted activity reports all-zero ids. Writing those to an event is worse than writing nothing:
        // it produces a tracing context that looks present but resolves to no trace at all.
        using var activity = _source.CreateActivity("append", ActivityKind.Client);

        await Assert.That(activity!.TraceId.ToHexString()).IsEqualTo(RecordedTrace.DefaultTraceId);

        var metadata          = new Metadata().AddActivityTags(activity);
        var (traceId, spanId) = metadata.GetTracingMeta();

        await Assert.That(traceId).IsNull();
        await Assert.That(spanId).IsNull();
    }

    [Test]
    public async Task ShouldNotOverrideExistingTracingMeta() {
        using var activity = _source.StartActivity("append");

        var metadata = new Metadata()
            .With(DiagnosticTags.TraceId, "0af7651916cd43dd8448eb211c80319c")
            .With(DiagnosticTags.SpanId, "b7ad6b7169203331")
            .AddActivityTags(activity);

        var (traceId, spanId) = metadata.GetTracingMeta();

        await Assert.That(traceId).IsEqualTo("0af7651916cd43dd8448eb211c80319c");
        await Assert.That(spanId).IsEqualTo("b7ad6b7169203331");
    }

    public void Dispose() {
        _listener.Dispose();
        _source.Dispose();
    }
}
