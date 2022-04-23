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

    const string MessageTypeHeader = "message-type";
    const string ContentTypeHeader = "content-type";

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
                .AddHeader(MessageTypeHeader, serialized.EventType)
                .AddHeader(ContentTypeHeader, serialized.ContentType);

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
                    _producerWithKey.Produce(stream, message, DeliveryHandler);
                }

                void DeliveryHandler(DeliveryReport<string, byte[]> report) => Report(report.Error);
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
                    _producerWithoutKey.Produce(stream, message, DeliveryHandler);
                }

                void DeliveryHandler(DeliveryReport<Null, byte[]> report) => Report(report.Error);
            }

            void Report(Error error) {
                // TODO: Handle error
                if (!error.IsError) {
                    Task.Run(() => producedMessage.OnAck().NoContext(), cancellationToken).GetAwaiter().GetResult();
                }
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken) {
        ReadyNow();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _producerWithKey.Flush(TimeSpan.FromSeconds(10));
        return Task.CompletedTask;
    }
}
