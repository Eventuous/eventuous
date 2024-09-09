using Confluent.Kafka;
using Eventuous.Kafka;
using Eventuous.Kafka.Producers;
using Eventuous.Producers;
using Eventuous.Tests.Subscriptions.Base;
using Eventuous.Tools;
using static System.String;
using static Eventuous.DeserializationResult;

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

        var events = Auto.CreateMany<TestEvent>().ToArray();

        await Produce();

        var consumed = new List<TestEvent>();
        await ExecuteConsume().NoThrow();
        _output.WriteLine($"Consumed {consumed.Count} events");
        consumed.Should().BeEquivalentTo(events);

        return;

        async Task Produce() {
            await using var producer = new KafkaBasicProducer(new(new() { BootstrapServers = _fixture.BootstrapServers }));
            await producer.StartAsync(default);
            await producer.Produce(new(topicName), events, new(), new("test"));
        }

        async Task ExecuteConsume() {
            using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var consumer = GetConsumer(topicName);
            consumer.Subscribe(topicName);

            while (!cts.IsCancellationRequested) {
                await Consume(consumer, cts.Token);

                if (consumed.Count == events.Length) break;
            }
        }

        async Task Consume(IConsumer<string, byte[]> c, CancellationToken cancellationToken) {
            var msg = c.Consume(cancellationToken);

            if (msg == null) {
                await Task.Delay(100, cancellationToken);

                return;
            }

            var meta = msg.Message.Headers.AsMetadata();

            var messageType = meta[KafkaHeaderKeys.MessageTypeHeader] as string;
            var contentType = meta[KafkaHeaderKeys.ContentTypeHeader] as string;

            var result = DefaultEventSerializer.Instance.DeserializeEvent(msg.Message.Value, messageType!, contentType!) as SuccessfullyDeserialized;

            var evt = (result!.Payload as TestEvent)!;
            _output.WriteLine($"Consumed {evt}");
            consumed.Add(evt);
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
                    var remaining = c.Assignment.Where(atp => partitions.All(rtp => rtp.TopicPartition != atp));

                    _output.WriteLine(
                        $"Partitions incrementally revoked: [{Join(',', partitions.Select(p => p.Partition.Value))}], remaining: [{Join(',', remaining.Select(p => p.Partition.Value))}]"
                    );
                }
            )
            .SetPartitionsLostHandler((_, partitions) => _output.WriteLine($"Partitions were lost: [{Join(", ", partitions)}]"))
            .Build();
    }
}
