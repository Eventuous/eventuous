using System.Diagnostics;
using Eventuous.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Eventuous.Tests.Diagnostics;

[NotInParallel]
public class ActivitySourceTests {
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ShouldHonorDroppedSpansRegardlessOfInitializationOrder(bool initializeSourceFirst) {
        Activity.Current = null;
        using var diagnostics = new IsolatedDiagnostics();
        if (initializeSourceFirst) _ = diagnostics.Source;

        using var provider = diagnostics.AddTracing(Sdk.CreateTracerProviderBuilder())
            .SetSampler(new AlwaysOffSampler())
            .Build();

        using var activity = diagnostics.Source.StartActivity("poll", ActivityKind.Client, default(ActivityContext));

        await Assert.That(activity is { IsAllDataRequested: true }).IsFalse();
        await Assert.That(activity is { Recorded: true }).IsFalse();
    }

    [Test]
    public async Task ShouldKeepFallbackContextPropagationWithoutOpenTelemetry() {
        Activity.Current = null;
        using var diagnostics = new IsolatedDiagnostics();
        using var activity = diagnostics.Source.StartActivity("publish", ActivityKind.Producer, default(ActivityContext));

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.TraceId).IsNotEqualTo(default(ActivityTraceId));
        await Assert.That(activity.IsAllDataRequested).IsTrue();
        await Assert.That(activity.Recorded).IsFalse();
    }

    [Test]
    public async Task ShouldKeepSampledSpansAfterRepeatedRegistration() {
        Activity.Current = null;
        using var diagnostics = new IsolatedDiagnostics();
        var builder = diagnostics.AddTracing(Sdk.CreateTracerProviderBuilder());
        using var provider = diagnostics.AddTracing(builder)
            .SetSampler(new AlwaysOnSampler())
            .Build();

        using var activity = diagnostics.Source.StartActivity("publish", ActivityKind.Producer, default(ActivityContext));

        await Assert.That(activity).IsNotNull();
        await Assert.That(activity!.IsAllDataRequested).IsTrue();
        await Assert.That(activity.Recorded).IsTrue();
    }

    [Test]
    public async Task ShouldReportAssemblyVersionForTracesAndMetrics() {
        using var diagnostics = new IsolatedDiagnostics();
        using var meter = diagnostics.GetMeter("version.test");
        var version = typeof(EventuousDiagnostics).Assembly.GetName().Version!.ToString();

        await Assert.That(diagnostics.Source.Version).IsEqualTo(version);
        await Assert.That(meter.Version).IsEqualTo(version);
    }
}
