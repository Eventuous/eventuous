namespace Eventuous.Kafka.Producers;

using Confluent.Kafka;
using Eventuous.Producers;
using Microsoft.Extensions.Hosting;
using System.Text;

using static System.TimeSpan;

public class KafkaProducer : BaseProducer<KafkaProduceOptions>, IHostedService, IAsyncDisposable {
    public KafkaProducer(KafkaProducerOptions options)
    {
        ConfluentProducer = new ProducerBuilder<byte[], object?>(options.Configuration).Build();
    }

    IProducer<byte[], object?> ConfluentProducer { get; }

    public long InFlightMessages => Interlocked.Read(ref _inFlightMessages);

    long _inFlightMessages;

    /// <inheritdoc />
    public override async Task ProduceMessages(string stream, IEnumerable<ProducedMessage> messages, KafkaProduceOptions? options, CancellationToken cancellationToken = default)
    {
        foreach (var message in messages)
            await ProduceMessage(stream, message, options, cancellationToken);
    }

    async Task ProduceMessage(string topic, ProducedMessage message, KafkaProduceOptions? options, CancellationToken cancellationToken = default)
    {
        var kafkaMessage = CreateKafkaMessage(message, options, "producer-name");

        Interlocked.Increment(ref _inFlightMessages);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                ConfluentProducer.Produce(topic, kafkaMessage, deliveryReport =>
                {
                    Interlocked.Decrement(ref _inFlightMessages);

                    if (deliveryReport.Error.IsError)
                        throw new ProduceException<byte[], object?>(
                            new Error(
                                deliveryReport.Error.Code,
                                $"{(deliveryReport.Error.IsFatal ? "Fatal Error " : "")}{deliveryReport.Error.Reason}",
                                deliveryReport.Error.IsFatal
                            ),
                            deliveryReport
                        );
                });
            }
            catch (Exception ex)
            {
                if (ex is KafkaException kex && kex.Error.Code == ErrorCode.Local_QueueFull)
                {
                    // An immediate failure of the produce call is most often caused by the
                    // local message queue being full, and appropriate response to that is
                    // to retry.

                    ConfluentProducer.Poll(FromMilliseconds(1000)); // TODO SV: configurable?
                    continue;
                }

                Interlocked.Decrement(ref _inFlightMessages);
                throw;
            }

            break;
        }
    }

    static Message<byte[], object?> CreateKafkaMessage(ProducedMessage message, KafkaProduceOptions options, string producerName)
    {
        if (!options.Headers.ContainsKey(HeaderKeys.RequestId))
            options.Headers[HeaderKeys.RequestId] = options.RequestId.ToString();

        if (!options.Headers.ContainsKey(HeaderKeys.MessageType))
            options.Headers[HeaderKeys.MessageType] = options.MessageType.Name;

        if (!options.Headers.ContainsKey(HeaderKeys.ProducerName))
            options.Headers[HeaderKeys.ProducerName] = producerName;

        if (!options.Headers.ContainsKey(HeaderKeys.MessageEncoding))
            options.Headers[HeaderKeys.MessageEncoding] = options.Encoding ?? ""; //options.Encoding ?? defaultContentType ?? "";

        if (message.Metadata != null)
            foreach (var (key, value) in message.Metadata)
                options.Headers.Add(key, value.ToString());

        var kafkaMessage = new Message<byte[], object?> {
            Value     = message.Message,
            Headers   = options.Headers.Encode(),
            Timestamp = new Timestamp(options.Timestamp)
        };

        if (options.MessageKey is not null)
        {
            options.Headers[HeaderKeys.MessageKey] = options.MessageKey;
            kafkaMessage.Key = Encoding.UTF8.GetBytes(options.MessageKey);
        }

        return kafkaMessage;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        ReadyNow();
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
        => await DisposeAsync().ConfigureAwait(false);

    public async ValueTask DisposeAsync()
    {
        // in some cases disposing actually locks forever...
        // until we understand why, let's cancel after 60 seconds
        using var cts = new CancellationTokenSource(FromSeconds(60));

        try
        {
            await Task.Run(() => ConfluentProducer.Dispose(), cts.Token);
        }
        catch (Exception ex)
        {
            if (InFlightMessages > 0)
                throw new Exception($"Failed to flush producer. {InFlightMessages} in flight messages dropped.", ex);

            throw;
        }
    }
}