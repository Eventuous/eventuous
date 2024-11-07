// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;

namespace Eventuous.Kafka.Producers;

/// <summary>
/// Produces messages with byte[] payload without using the schema registry. The message type is specified in the
/// headers, so the type mapping is required.
/// </summary>
public class KafkaBasicProducer : BaseProducer<KafkaProduceOptions>, IHostedProducer, IAsyncDisposable {
    readonly IProducer<string, byte[]> _producerWithKey;
    readonly IProducer<Null, byte[]>   _producerWithoutKey;
    readonly IEventSerializer          _serializer;

    public KafkaBasicProducer(KafkaProducerOptions options, IEventSerializer? serializer = null)
        : base(TracingOptions) {
        _producerWithKey    = new ProducerBuilder<string, byte[]>(options.ProducerConfig).Build();
        _producerWithoutKey = new DependentProducerBuilder<Null, byte[]>(_producerWithKey.Handle).Build();
        _serializer         = serializer ?? DefaultEventSerializer.Instance;
    }

    static readonly ProducerTracingOptions TracingOptions = new() {
        MessagingSystem  = "kafka",
        DestinationKind  = "topic",
        ProduceOperation = "produce"
    };

    protected override async Task ProduceMessages(
            StreamName                   stream,
            IEnumerable<ProducedMessage> messages,
            KafkaProduceOptions?         options,
            CancellationToken            cancellationToken = default
        ) {
        foreach (var producedMessage in messages) {
            var serialized = _serializer.SerializeEvent(producedMessage.Message);
            var headers    = producedMessage.Metadata?.AsKafkaHeaders() ?? [];

            headers
                .AddHeader(KafkaHeaderKeys.MessageTypeHeader, serialized.EventType)
                .AddHeader(KafkaHeaderKeys.ContentTypeHeader, serialized.ContentType);

            SetActivityMessageType(serialized.EventType);
            await ProduceLocal();

            continue;

            Task ProduceLocal() => options?.PartitionKey != null
                ? ProduceInt(options.PartitionKey, _producerWithKey)
                : ProduceInt(null, _producerWithoutKey);

            async Task ProduceInt<TKey>(TKey? key, IProducer<TKey, byte[]> producer) {
                var message = new Message<TKey, byte[]> {
                    Value   = serialized.Payload,
                    Headers = headers
                };
                if (key != null) message.Key = key;

                if (producedMessage.OnAck == null) {
                    await producer.ProduceAsync(stream, message, cancellationToken).NoContext();
                }
                else {
                    producer.Produce(stream, message, report => Report(producedMessage, report.Error));
                }
            }

            static void Report(ProducedMessage msg, Error error)
                => AwaitValueTask(error.IsError ? msg.Nack<KafkaBasicProducer>(error.Reason, null) : msg.Ack<KafkaBasicProducer>());

            static void AwaitValueTask(ValueTask valueTask) {
                if (valueTask.IsCompletedSuccessfully) return;

                valueTask.AsTask().GetAwaiter().GetResult();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        Ready = true;

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        _stopping = true;
        Ready     = false;

        while (!cancellationToken.IsCancellationRequested) {
            var count = _producerWithKey.Flush(TimeSpan.FromSeconds(10));

            if (count == 0) break;

            await Task.Delay(100, cancellationToken).NoContext();
        }

        _stopping = false;
    }

    public bool Ready { get; private set; }

    bool _stopping;

    public async ValueTask DisposeAsync() {
        if (Ready && !_stopping) await StopAsync(default);
        while (_stopping || Ready) await Task.Delay(100).NoContext();

        await CastAndDispose(_producerWithKey);
        await CastAndDispose(_producerWithoutKey);

        GC.SuppressFinalize(this);

        return;

        static async ValueTask CastAndDispose(IDisposable resource) {
            if (resource is IAsyncDisposable resourceAsyncDisposable)
                await resourceAsyncDisposable.DisposeAsync();
            else
                resource.Dispose();
        }
    }
}
