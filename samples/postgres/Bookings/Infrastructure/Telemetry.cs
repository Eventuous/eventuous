using Eventuous.Diagnostics.OpenTelemetry;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookings.Infrastructure;

public static class Telemetry {
    public static void AddTelemetry(this IServiceCollection services) {
        var otelEnabled = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") != null;

        services.AddOpenTelemetry()
            .ConfigureResource(builder => builder.AddService("bookings"))
            .WithMetrics(
                builder => {
                    builder
                        .AddAspNetCoreInstrumentation()
                        .AddEventuous()
                        .AddEventuousSubscriptions()
                        .AddPrometheusExporter();
                    if (otelEnabled) builder.AddOtlpExporter();
                }
            );

        services.AddOpenTelemetry()
            .WithTracing(
                builder => {
                    builder
                        .AddAspNetCoreInstrumentation()
                        .AddEventuousTracing()
                        .AddNpgsql()
                        .SetSampler(new PostgresPollingSampler());

                    if (otelEnabled)
                        builder.AddOtlpExporter();
                    else
                        builder.AddZipkinExporter();
                }
            );
    }
}

class PostgresPollingSampler : Sampler {
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) {
        if (samplingParameters.Tags == null) return new SamplingResult(SamplingDecision.RecordAndSample);

        return samplingParameters.Tags.Any(
            t => t.Key == "db.statement" && t.Value is string str && str.Contains("read_all_forwards")
        )
            ? new SamplingResult(SamplingDecision.Drop)
            : new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
