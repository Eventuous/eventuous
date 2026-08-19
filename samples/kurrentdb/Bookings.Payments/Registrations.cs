using System.Text.Json;
using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Bookings.Payments.Integration;
using Eventuous;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.KurrentDB;
using Eventuous.KurrentDB.Producers;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Projections.MongoDB;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookings.Payments;

public static class Registrations {
    extension(IServiceCollection services) {
        public void AddServices(IConfiguration configuration) {
            EventSerializer.SetDefault(new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web)));

            services.AddKurrentDBClient(configuration["KurrentDB:ConnectionString"]!);
            services.AddEventStore<KurrentDBEventStore>();
            services.AddCommandService<CommandService, PaymentState>();
            services.AddSingleton(Mongo.ConfigureMongo(configuration));
            services.AddCheckpointStore<MongoCheckpointStore>();
            services.AddProducer<KurrentDBProducer>();

            services
                .AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions, KurrentDBProducer, KurrentDBProduceOptions>(
                    "IntegrationSubscription",
                    PaymentsGateway.Transform
                );
        }

        public void AddTelemetry() {
            var otelEnabled = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") != null;

            services.AddOpenTelemetry()
                .WithMetrics(
                    builder => {
                        builder
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("payments"))
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
                            .AddGrpcClientInstrumentation()
                            .AddEventuousTracing()
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("payments"))
                            .SetSampler(new AlwaysOnSampler());

                        if (otelEnabled)
                            builder.AddOtlpExporter();
                        else
                            builder.AddZipkinExporter();
                    }
                );
        }
    }
}
