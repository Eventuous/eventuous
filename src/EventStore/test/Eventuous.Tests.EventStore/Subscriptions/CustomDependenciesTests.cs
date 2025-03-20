// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.EventStore.Producers;
using Eventuous.EventStore.Subscriptions;
using Eventuous.Producers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Registrations;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Subscriptions;

public class CustomDependenciesTests {
    readonly TestSerializer      _serializer      = new();
    readonly TestCheckpointStore _checkpointStore = new();
    IHostedService               _service         = null!;
    EventStoreDbContainer        _container       = null!;
    readonly StreamName          _streamName      = new($"test-{Guid.NewGuid():N}");
    IProducer                    _producer        = null!;
    TestEventHandler             _handler         = null!;

    [Before(Test)]
    public async Task Setup(CancellationToken cancellationToken) {
        _container = EsdbContainer.Create();
        var services = new ServiceCollection();
        await _container.StartAsync(cancellationToken);

        services.AddLogging(b => b.AddFilter("Grpc", LogLevel.Error).AddConsole().AddTUnit(LogLevel.Debug));

        services.AddEventStoreClient(_container.GetConnectionString());
        services.AddProducer<EventStoreProducer>();

        // Add NoOp store globally to make sure it's not picked up
        services.AddCheckpointStore<NoOpCheckpointStore>();

        _handler = new();
        var typeMapper = new TypeMapper();
        typeMapper.AddType<TestEvent>();

        services.AddSubscription<StreamSubscription, StreamSubscriptionOptions>(
            "test-custom",
            b => b
                .Configure(cfg => cfg.StreamName = _streamName)
                .UseCheckpointStore<TestCheckpointStore>(_ => _checkpointStore)
                .UseSerializer<TestSerializer>(_ => _serializer)
                .UseTypeMapper(typeMapper)
                .AddEventHandler(_handler)
        );

        var provider = services.BuildServiceProvider();

        _producer = provider.GetRequiredService<IProducer>();
        _service  = provider.GetRequiredService<IHostedService>();

        await _service.StartAsync(cancellationToken);
    }

    [After(Test)]
    public async Task Shutdown(CancellationToken cancellationToken) {
        await _service.StopAsync(cancellationToken);
        await _container.StopAsync(cancellationToken);
    }

    [Test]
    public async Task ShouldUseCustomDependencies(CancellationToken cancellationToken) {
        var message = TestEvent.Create();
        await _producer.Produce(_streamName, message, new(), cancellationToken: cancellationToken);

        while (_handler.Message == null) {
            await Task.Delay(100, cancellationToken);
        }

        await Assert.That(_handler.Message).IsTypeOf<TestEvent>();
        await Assert.That(_handler.Message).IsEqualTo(message with {Number = message.Number + 1});
    }

    class TestEventHandler : IEventHandler {
        public string DiagnosticName => nameof(TestEventHandler);

        public object? Message { get; private set; }

        public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
            Message = context.Message;

            return ValueTask.FromResult(EventHandlingStatus.Handled);
        }
    }

    class TestCheckpointStore : ICheckpointStore {
        public bool ReceivedGetCheckpoint { get; private set; }

        public ValueTask<Checkpoint> GetLastCheckpoint(string checkpointId, CancellationToken cancellationToken) {
            ReceivedGetCheckpoint = true;

            return ValueTask.FromResult(new Checkpoint(checkpointId, null));
        }

        public ValueTask<Checkpoint> StoreCheckpoint(Checkpoint checkpoint, bool force, CancellationToken cancellationToken) {
            return ValueTask.FromResult(checkpoint);
        }
    }

    class TestSerializer : IEventSerializer {
        public DeserializationResult DeserializeEvent(ReadOnlySpan<byte> data, string eventType, string contentType) {
            var result = DefaultEventSerializer.Instance.DeserializeEvent(data, eventType, contentType);

            if (result is not DeserializationResult.SuccessfullyDeserialized { Payload: var evt }) {
                return result;
            }

            return evt is not TestEvent testEvent 
                ? result 
                : new DeserializationResult.SuccessfullyDeserialized(testEvent with { Number = testEvent.Number + 1 });
        }

        public SerializationResult SerializeEvent(object evt) {
            throw new NotImplementedException();
        }
    }
}
