using Eventuous.Azure.ServiceBus.Producers;
using Eventuous.Azure.ServiceBus.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.ServiceBus;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Azure.ServiceBus;

public class AzureServiceBusFixture : IAsyncInitializer, IAsyncDisposable {
    public ServiceBusClient Client           { get; private set; } = null!;
    public string           ConnectionString { get; private set; } = null!;
    public ServiceBusContainer Container { get; } = new ServiceBusBuilder()
        .WithImage("mcr.microsoft.com/azure-messaging/servicebus-emulator:latest")
        .WithAcceptLicenseAgreement(true)
        .Build();

    public async Task InitializeAsync() {
        await Container.StartAsync();

        ConnectionString = Container.GetConnectionString();

        Client = new(
            ConnectionString,
            new ServiceBusClientOptions {
                TransportType = ServiceBusTransportType.AmqpTcp
            }
        );
    }

    public async ValueTask DisposeAsync() {
        await Client.DisposeAsync();
        await Container.DisposeAsync();
    }

    public ServiceBusProducer CreateProducer(ServiceBusProducerOptions options) =>
        new(Client, options, serializer: null, log: NullLogger<ServiceBusProducer>.Instance);

    public ServiceBusSubscription CreateSubscription(ServiceBusSubscriptionOptions options, IEventHandler handler, string correlationId)
        => new(Client, options, new ConsumePipe().AddFilterFirst(FilterOnCorrelationId(correlationId)).AddDefaultConsumer(handler), null, null);

    /// <summary>
    /// So that we can use the same service bus subscription for multiple tests.
    /// Probably serves no purpose, but it's a nice pattern to have.
    /// </summary>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    static MessageFilter FilterOnCorrelationId(string correlationId) =>
        new(message => message.Metadata?[MetaTags.CorrelationId]?.ToString() == correlationId);
}
