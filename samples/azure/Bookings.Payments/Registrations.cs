using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Integration;
using Eventuous.Azure.ServiceBus.Producers;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Microsoft.Extensions.Azure;

namespace Bookings.Payments;

public static class Registrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        services.AddAzureClients(async builder => {
            var sbConnectionString = configuration.GetConnectionString("sbemulators") ?? throw new InvalidOperationException("Connection string 'sbemulators' not found.");
            builder.AddServiceBusClient(sbConnectionString);
            var blobConnectionString = configuration.GetConnectionString("blobs") ?? throw new InvalidOperationException("Connection string 'blobs' not found.");
            builder.AddBlobServiceClient(blobConnectionString);
        });

        var connectionString = configuration.GetConnectionString("payments-db") ?? throw new InvalidOperationException("Connection string 'payments-db' not found.");

        services.AddEventuousSqlServer(connectionString, initializeDatabase: true);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerCheckpointStore();
        services.AddCommandService<CommandService, PaymentState>();
        services.AddProducer<ServiceBusProducer>();
        services.AddSingleton(new ServiceBusProducerOptions {
            QueueOrTopicName = "PaymentsIntegration",
        });

        services
            .AddGateway<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, ServiceBusProducer, ServiceBusProduceOptions>(
                "IntegrationSubscription",
                PaymentsGateway.Transform
            );
    }
}
