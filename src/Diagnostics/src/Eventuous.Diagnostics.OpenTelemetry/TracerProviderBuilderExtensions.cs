// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace Eventuous.Diagnostics.OpenTelemetry;

[PublicAPI]
public static class TracerProviderBuilderExtensions {
    /// <summary>
    /// Adds an Eventuous activity source to OpenTelemetry trace collection
    /// </summary>
    /// <param name="builder"><seealso cref="TracerProviderBuilder"/> instance</param>
    /// <returns></returns>
    public static TracerProviderBuilder AddEventuousTracing(this TracerProviderBuilder builder) {
        // The DummyListener is added by default, so the remote context is propagated regardless.
        // After adding the activity source to OpenTelemetry, we don't need a fake listener.
        EventuousDiagnostics.RemoveDummyListener();

        return Ensure.NotNull(builder).AddSource(EventuousDiagnostics.InstrumentationName).SetSampler(new PollingSampler());
    }

    class PollingSampler : Sampler {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) {
            return samplingParameters.ParentContext is { TraceFlags: ActivityTraceFlags.None } && samplingParameters is { Kind: ActivityKind.Client, Name: "eventuous" }
                ? new SamplingResult(SamplingDecision.Drop)
                : new SamplingResult(SamplingDecision.RecordAndSample);
        }
    }
}
