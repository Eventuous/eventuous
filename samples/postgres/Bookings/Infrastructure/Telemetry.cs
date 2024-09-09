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
                        .AddMongoDBInstrumentation();

                    if (otelEnabled)
                        builder.AddOtlpExporter();
                    else
                        builder.AddZipkinExporter();
                }
            );
    }
}