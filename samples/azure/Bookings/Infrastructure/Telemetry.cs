using Eventuous.Diagnostics.OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookings.Infrastructure;

public static class Telemetry {
    public static void AddTelemetry(this IServiceCollection services) {
        if (Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") == null)
            return;

        services.AddOpenTelemetry()
            .ConfigureResource(builder => builder.AddService("bookings"))
            .WithMetrics(
                builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddSqlClientInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEventuous()
                        .AddEventuousSubscriptions()
                        .AddPrometheusExporter()
                        .AddOtlpExporter())
            .WithTracing(
                builder => builder
                        .AddAspNetCoreInstrumentation()
                        .AddSqlClientInstrumentation()
                        .AddHttpClientInstrumentation()
                        .AddEventuousTracing()
                        .AddOtlpExporter());
    }
}