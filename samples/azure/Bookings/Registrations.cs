using System.Text.Json;
using Bookings.Application;
using Bookings.Application.Queries;
using Bookings.Domain;
using Bookings.Domain.Bookings;
using Bookings.Infrastructure;
using Bookings.Integration;
using Eventuous;
using Eventuous.Azure.ServiceBus.Subscriptions;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Microsoft.Extensions.Azure;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Bookings;

public static class Registrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        DefaultEventSerializer.SetDefaultSerializer(
            new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb))
        );

        services.AddAzureClients(async builder => {
            var sbConnectionString = configuration.GetConnectionString("sbemulators") ?? throw new InvalidOperationException("Connection string 'sbemulators' not found.");
            builder.AddServiceBusClient(sbConnectionString);
            var blobConnectionString = configuration.GetConnectionString("blobs") ?? throw new InvalidOperationException("Connection string 'blobs' not found.");
            builder.AddBlobServiceClient(blobConnectionString);
        });

        var connectionString = configuration.GetConnectionString("database") ?? throw new InvalidOperationException("Connection string 'database' not found.");

        services.AddEventuousSqlServer(connectionString, "b", true);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerCheckpointStore();
        services.AddCommandService<BookingsCommandService, BookingState>();

        services.AddSingleton<Services.IsRoomAvailable>((_, _) => new(true));

        services.AddSingleton<Services.ConvertCurrency>(
            (from, currency) => new(from.Amount * 2, currency)
        );

        services.AddSingleton(Mongo.ConfigureMongo(configuration));

        services.AddSubscription<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions>(
            "BookingsProjections",
            builder => builder
                .AddEventHandler<BookingStateProjection>()
                .AddEventHandler<MyBookingsProjection>()
        );

        services.AddSubscription<ServiceBusSubscription, ServiceBusSubscriptionOptions>(
            "PaymentIntegration",
            builder => builder
                .Configure(x => x.QueueOrTopic = new Queue(PaymentsIntegrationHandler.Stream))
                .AddEventHandler<PaymentsIntegrationHandler>()
        );
    }
}
