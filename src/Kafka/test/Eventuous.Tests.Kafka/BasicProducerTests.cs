using Confluent.Kafka;
using Eventuous.Kafka;
using Eventuous.Kafka.Producers;
using Eventuous.Producers;
using Eventuous.Sut.Subs;
using static System.String;

namespace Eventuous.Tests.Kafka;

public class BasicProducerTests : IClassFixture<KafkaFixture> {
    readonly KafkaFixture      _fixture;
    readonly ITestOutputHelper _output;

    public BasicProducerTests(KafkaFixture fixture, ITestOutputHelper output) {
        _fixture = fixture;
        _output  = output;
        TypeMap.Instance.AddType<TestEvent>("testEvent");
    }

    static readonly Fixture Auto = new();

    [Fact]
    public async Task ShouldProduceAndWait() {
        var topicName = Auto.Create<string>();
        _output.WriteLine($"Topic: {topicName}");

        var producer = new KafkaBasicProducer(
            new KafkaProducerOptions(new ProducerConfig { BootstrapServers = _fixture.BootstrapServers })
        );

        var produced = new List<TestEvent>();

        var events = Auto.CreateMany<TestEvent>().ToArray();
        await producer.StartAsync(default);

        await producer.Produce(new StreamName(topicName), events, new Metadata(), new KafkaProduceOptions("test"), onAck: OnAck);

        await producer.StopAsync(default);

        produced.Should().BeEquivalentTo(events);

        using var consumer = GetConsumer(topicName);

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        consumer.Subscribe(topicName);

        var consumed = new List<TestEvent>();

        try {
            while (!cts.IsCancellationRequested) {
                var msg = consumer.Consume(cts.Token);

                if (msg == null) return;

                var meta = msg.Message.Headers.AsMetadata();

                var messageType = meta[KafkaHeaderKeys.MessageTypeHeader] as string;
                var contentType = meta[KafkaHeaderKeys.ContentTypeHeader] as string;

                var result = DefaultEventSerializer.Instance.DeserializeEvent(msg.Message.Value, messageType!, contentType!) as SuccessfullyDeserialized;

                var evt = (result!.Payload as TestEvent)!;
                _output.WriteLine($"Consumed {evt}");
                consumed.Add(evt);

                if (consumed.Count == events.Length) break;
            }
        } catch (OperationCanceledException) {
            // ignore
        }

        _output.WriteLine($"Consumed {consumed.Count} events");
        consumed.Should().BeEquivalentTo(events);

        return;

        ValueTask OnAck(ProducedMessage msg) {
            _output.WriteLine("Produced message: {0}", msg.Message);
            produced.Add((TestEvent)msg.Message);

            return ValueTask.CompletedTask;
        }
    }

    IConsumer<string, byte[]> GetConsumer(string groupId) {
        var config = new ConsumerConfig {
            BootstrapServers            = _fixture.BootstrapServers,
            GroupId                     = groupId,
            EnableAutoCommit            = false,
            StatisticsIntervalMs        = 5000,
            SessionTimeoutMs            = 6000,
            AutoOffsetReset             = AutoOffsetReset.Earliest,
            EnablePartitionEof          = true,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        return new ConsumerBuilder<string, byte[]>(config)
            .SetErrorHandler((_,      e) => _output.WriteLine($"Error: {e.Reason}"))
            .SetStatisticsHandler((_, json) => _output.WriteLine($"Statistics: {json}"))
            .SetPartitionsAssignedHandler(
                (c, partitions) => {
                    _output.WriteLine(
                        $"Partitions incrementally assigned: [{Join(',', partitions.Select(p => p.Partition.Value))}], all: [{Join(',', c.Assignment.Concat(partitions).Select(p => p.Partition.Value))}]"
                    );
                }
            )
            .SetPartitionsRevokedHandler(
                (c, partitions) => {
                    var remaining = c.Assignment.Where(
                        atp => partitions.All(rtp => rtp.TopicPartition != atp)
                    );

                    _output.WriteLine(
                        $"Partitions incrementally revoked: [{Join(',', partitions.Select(p => p.Partition.Value))}], remaining: [{Join(',', remaining.Select(p => p.Partition.Value))}]"
                    );
                }
            )
            .SetPartitionsLostHandler(
                (_, partitions) => _output.WriteLine($"Partitions were lost: [{Join(", ", partitions)}]")
            )
            .Build();
    }
}
