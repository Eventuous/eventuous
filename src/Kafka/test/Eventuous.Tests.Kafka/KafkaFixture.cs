using Testcontainers.Kafka;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Kafka;

public class KafkaFixture : IAsyncInitializer, IAsyncDisposable {
    KafkaContainer _kafkaContainer = null!;

    public async Task InitializeAsync() {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.2.6")
            .Build();
        await _kafkaContainer.StartAsync();
    }

    public string BootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public async ValueTask DisposeAsync() => await _kafkaContainer.DisposeAsync();
}
