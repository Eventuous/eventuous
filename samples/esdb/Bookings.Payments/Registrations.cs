using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Infrastructure;
using Bookings.Payments.Integration;
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
    public static void AddServices(this IServiceCollection services, IConfiguration configuration) {
        services.AddKurrentDBClient(configuration["EventStore:ConnectionString"]!);
        services.AddEventStore<KurrentDBEventStore>();
        services.AddCommandService<CommandService, PaymentState>();
        services.AddSingleton(Mongo.ConfigureMongo(configuration));
        services.AddCheckpointStore<MongoCheckpointStore>();
        services.AddProducer<KurrentDbProducer>();

        services
            .AddGateway<AllStreamSubscription, AllStreamSubscriptionOptions, KurrentDbProducer, KurrentDbProduceOptions>(
                "IntegrationSubscription",
                PaymentsGateway.Transform
            );
    }

    public static void AddTelemetry(this IServiceCollection services) {
        services.AddOpenTelemetry()
            .WithMetrics(
                builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddEventuous()
                    .AddEventuousSubscriptions()
                    .AddPrometheusExporter()
            );

        services.AddOpenTelemetry()
            .WithTracing(
                builder => builder
                    .AddAspNetCoreInstrumentation()
                    .AddGrpcClientInstrumentation()
                    .AddEventuousTracing()
                    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("payments"))
                    .SetSampler(new AlwaysOnSampler())
                    .AddZipkinExporter()
            );
    }
}
