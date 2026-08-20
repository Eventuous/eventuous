using System.Text.Json;
using Azure.Storage.Blobs;
using Bookings.Application;
using Bookings.Application.Queries;
using Bookings.Domain;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Bookings.Integration;
using Eventuous;
using Eventuous.Azure.Storage.Blobs;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.KurrentDB;
using Eventuous.KurrentDB.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.Subscriptions.Registrations;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Bookings;

public static class Registrations {
    extension(IServiceCollection services) {
        public void AddEventuous(IConfiguration configuration) {
            var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb);
            EventSerializer.SetDefault(new DefaultEventSerializer(jsonOptions));

            services.AddKurrentDBClient(configuration["KurrentDB:ConnectionString"]!);
            services.AddEventStore<KurrentDBEventStore>();
            services.AddCommandService<BookingsCommandService, BookingState>();

            services.AddSingleton<Services.IsRoomAvailable>((_,    _) => new(true));
            services.AddSingleton<Services.ConvertCurrency>((from, currency) => new(from.Amount * 2, currency));

            services.AddSingleton(Mongo.ConfigureMongo(configuration));

            services.AddSingleton(new BlobServiceClient(configuration.GetConnectionString("blobs")));
            services.AddSingleton(
                new BlobStorageProjectorOptions {
                    JsonOptions     = jsonOptions,
                    RaceRetries     = 3,
                    IdempotencyMode = IdempotencyMode.ByGlobalPosition
                }
            );

            services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                "BookingsProjections",
                builder => builder
                    .UseCheckpointStore<MongoCheckpointStore>()
                    .AddEventHandler<BookingStateProjection>()
                    .AddEventHandler<MyBookingsProjection>()
                    .WithPartitioningByStream(2)
            );

            // The blob projection runs on its own subscription with its own checkpoint, so when
            // it's added to a system with existing data, it replays all events from the beginning
            // and backfills the blobs instead of starting from the other projections' position
            services.AddSubscription<AllStreamSubscription, AllStreamSubscriptionOptions>(
                "BookingsBlobProjection",
                builder => builder
                    .UseCheckpointStore<MongoCheckpointStore>()
                    .AddEventHandler<BookingStateBlobProjection>()
            );
            services.AddSingleton<BookingsQueryService>();

            services.AddSubscription<StreamPersistentSubscription, StreamPersistentSubscriptionOptions>(
                "PaymentIntegration",
                builder => builder
                    .Configure(x => x.StreamName = PaymentsIntegrationHandler.Stream)
                    .AddEventHandler<PaymentsIntegrationHandler>()
            );
        }

        public void AddTelemetry() {
            var otelEnabled = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") != null;

            services.AddOpenTelemetry()
                .WithMetrics(
                    builder => {
                        builder
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("bookings"))
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
                            .AddSource(typeof(DiagnosticsActivityEventSubscriber).Assembly.GetName().Name!)
                            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("bookings"))
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
