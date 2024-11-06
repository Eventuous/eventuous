using Testcontainers.RabbitMq;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.RabbitMq;

public class RabbitMqFixture : IAsyncInitializer, IAsyncDisposable {
    RabbitMqContainer _rabbitMq = null!;

    public ConnectionFactory ConnectionFactory { get; private set; } = null!;

    public async Task InitializeAsync() {
        _rabbitMq = new RabbitMqBuilder().Build();
        await _rabbitMq.StartAsync();
        ConnectionFactory = new() { Uri = new(_rabbitMq.GetConnectionString()), DispatchConsumersAsync = true };
    }

    public async ValueTask DisposeAsync() => await _rabbitMq.DisposeAsync();
}
