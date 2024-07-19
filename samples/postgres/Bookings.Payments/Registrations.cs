using Bookings.Infrastructure;
using Bookings.Payments.Application;
using Bookings.Payments.Domain;
using Bookings.Payments.Integration;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Projections.MongoDB;
using Eventuous.RabbitMq.Producers;
using RabbitMQ.Client;

namespace Bookings.Payments;

public static class Registrations {
    public static void AddEventuous(this IServiceCollection services, IConfiguration configuration) {
        var connectionFactory = new ConnectionFactory {
            Uri                    = new Uri(configuration["RabbitMq:ConnectionString"]!),
            DispatchConsumersAsync = true
        };
        services.AddSingleton(connectionFactory);
        services.AddEventuousPostgres(configuration.GetSection("Postgres"));
        services.AddEventStore<PostgresStore>();
        services.AddCommandService<CommandService, PaymentState>();
        services.AddSingleton(Mongo.ConfigureMongo(configuration));
        services.AddCheckpointStore<MongoCheckpointStore>();
        services.AddProducer<RabbitMqProducer>();

        services
            .AddGateway<PostgresAllStreamSubscription, PostgresAllStreamSubscriptionOptions, RabbitMqProducer, RabbitMqProduceOptions>(
                "IntegrationSubscription",
                PaymentsGateway.Transform
            );
    }
}
