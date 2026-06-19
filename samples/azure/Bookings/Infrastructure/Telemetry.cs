using Eventuous.Diagnostics.OpenTelemetry;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
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
                        // .AddSqlClientInstrumentation() puzzle out why
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
                        // .AddSqlClientInstrumentation() puzzle out why
                        .AddEventuousTracing()
                        .AddSource(typeof(DiagnosticsActivityEventSubscriber).Assembly.GetName().Name!);

                    if (otelEnabled)
                        builder.AddOtlpExporter();
                    else
                        builder.AddZipkinExporter();
                }
            );
    }
}