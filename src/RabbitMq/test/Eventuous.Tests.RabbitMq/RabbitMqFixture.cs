using Testcontainers.RabbitMq;

namespace Eventuous.Tests.RabbitMq;

public class RabbitMqFixture : IAsyncLifetime {
    RabbitMqContainer _rabbitMq = null!;

    public ConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async ValueTask InitializeAsync() {
        _rabbitMq = new RabbitMqBuilder().Build();
        await _rabbitMq.StartAsync();
        ConnectionFactory = new ConnectionFactory { Uri = new Uri(_rabbitMq.GetConnectionString()), DispatchConsumersAsync = true };
    }

    public async ValueTask DisposeAsync() => await _rabbitMq.DisposeAsync();
}
