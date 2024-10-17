// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Testcontainers.Kafka;

namespace Eventuous.Tests.Kafka;

public class KafkaFixture : IAsyncLifetime {
    KafkaContainer _kafkaContainer = null!;

    public async ValueTask InitializeAsync() {
        _kafkaContainer = new KafkaBuilder()
            .WithImage("confluentinc/cp-kafka:7.2.6")
            .Build();
        await _kafkaContainer.StartAsync();
    }

    public string BootstrapServers => _kafkaContainer.GetBootstrapAddress();

    public async ValueTask DisposeAsync() => await _kafkaContainer.DisposeAsync();
}
