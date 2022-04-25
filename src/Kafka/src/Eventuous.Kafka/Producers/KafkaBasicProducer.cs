using Confluent.Kafka;
using Eventuous.Producers;
using Eventuous.Producers.Diagnostics;
using Microsoft.Extensions.Hosting;

namespace Eventuous.Kafka.Producers;

/// <summary>
/// Produces messages with byte[] payload without using the schema registry. The message type is specified in the
/// headers, so the type mapping is required.
/// </summary>
public class KafkaBasicProducer : BaseProducer<KafkaProduceOptions>, IHostedService {
    readonly IProducer<string, byte[]> _producerWithKey;
    readonly IProducer<Null, byte[]>   _producerWithoutKey;
    readonly IEventSerializer          _serializer;

    public KafkaBasicProducer(KafkaProducerOptions options, IEventSerializer? serializer = null) :
        base(TracingOptions) {
        _producerWithKey    = new ProducerBuilder<string, byte[]>(options.ProducerConfig).Build();
        _producerWithoutKey = new DependentProducerBuilder<Null, byte[]>(_producerWithKey.Handle).Build();

        _serializer = serializer ?? DefaultEventSerializer.Instance;
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
            var headers    = producedMessage.Metadata?.AsKafkaHeaders() ?? new Headers();

            headers
                .AddHeader(KafkaHeaderKeys.MessageTypeHeader, serialized.EventType)
                .AddHeader(KafkaHeaderKeys.ContentTypeHeader, serialized.ContentType);

            await ProduceLocal();

            Task ProduceLocal() => options?.PartitionKey != null ? ProducePartitioned() : ProduceNotPartitioned();

            async Task ProducePartitioned() {
                var message = new Message<string, byte[]> {
                    Value   = serialized.Payload,
                    Key     = options.PartitionKey,
                    Headers = headers
                };

                if (producedMessage.OnAck == null) {
                    await _producerWithKey.ProduceAsync(stream, message, cancellationToken).NoContext();
                }
                else {
                    _producerWithKey.Produce(stream, message, r => DeliveryHandler(r, producedMessage));
                }

                void DeliveryHandler(DeliveryReport<string, byte[]> report, ProducedMessage msg)
                    => Report(report.Error, msg);
            }

            async Task ProduceNotPartitioned() {
                var message = new Message<Null, byte[]> {
                    Value   = serialized.Payload,
                    Headers = headers
                };

                if (producedMessage.OnAck == null) {
                    await _producerWithoutKey.ProduceAsync(stream, message, cancellationToken).NoContext();
                }
                else {
                    _producerWithoutKey.Produce(stream, message, r => DeliveryHandler(r, producedMessage));
                }

                void DeliveryHandler(DeliveryReport<Null, byte[]> report, ProducedMessage msg)
                    => Report(report.Error, msg);
            }

            void Report(Error error, ProducedMessage message) {
                if (error.IsError) {
                    producedMessage.OnNack?.Invoke(message, error.Reason, null).NoContext().GetAwaiter().GetResult();
                }

                producedMessage.OnAck(message).NoContext().GetAwaiter().GetResult();
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        ReadyNow();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken) {
        while (!cancellationToken.IsCancellationRequested) {
            var count = _producerWithKey.Flush(TimeSpan.FromSeconds(10));
            if (count == 0) break;

            await Task.Delay(100, cancellationToken).NoContext();
        }
    }
}
