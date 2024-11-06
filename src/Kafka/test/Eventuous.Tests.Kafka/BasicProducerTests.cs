using Confluent.Kafka;
using Eventuous.Kafka;
using Eventuous.Kafka.Producers;
using Eventuous.Producers;
using Eventuous.Tests.Subscriptions.Base;
using Eventuous.Tools;
using static System.String;
using static Eventuous.DeserializationResult;

// ReSharper disable MethodHasAsyncOverload

namespace Eventuous.Tests.Kafka;

[ClassDataSource<KafkaFixture>]
public class BasicProducerTests {
    readonly KafkaFixture _fixture;

    public BasicProducerTests(KafkaFixture fixture) {
        _fixture = fixture;
        TypeMap.Instance.AddType<TestEvent>("testEvent");
    }

    static readonly Fixture Auto = new();

    [Test]
    public async Task ShouldProduceAndWait(CancellationToken cancellationToken) {
        var topicName = Auto.Create<string>();
        TestContext.Current?.OutputWriter.WriteLine($"Topic: {topicName}");

        var events = Auto.CreateMany<TestEvent>().ToArray();

        await Produce();

        var consumed = new List<TestEvent>();
        await ExecuteConsume().NoThrow();
        TestContext.Current?.OutputWriter.WriteLine($"Consumed {consumed.Count} events");
        consumed.Should().BeEquivalentTo(events);

        return;

        async Task Produce() {
            await using var producer = new KafkaBasicProducer(new(new() { BootstrapServers = _fixture.BootstrapServers }));
            await producer.StartAsync(cancellationToken);
            await producer.Produce(new(topicName), events, new(), new("test"), cancellationToken: cancellationToken);
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

        async Task Consume(IConsumer<string, byte[]> c, CancellationToken ct) {
            var msg = c.Consume(ct);

            if (msg == null) {
                await Task.Delay(100, ct);

                return;
            }

            var meta = msg.Message.Headers.AsMetadata();

            var messageType = meta[KafkaHeaderKeys.MessageTypeHeader] as string;
            var contentType = meta[KafkaHeaderKeys.ContentTypeHeader] as string;

            var result = DefaultEventSerializer.Instance.DeserializeEvent(msg.Message.Value, messageType!, contentType!) as SuccessfullyDeserialized;

            var evt = (result!.Payload as TestEvent)!;
            TestContext.Current?.OutputWriter.WriteLine($"Consumed {evt}");
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
            .SetErrorHandler((_,      e) => TestContext.Current?.OutputWriter.WriteLine($"Error: {e.Reason}"))
            .SetStatisticsHandler((_, json) => TestContext.Current?.OutputWriter.WriteLine($"Statistics: {json}"))
            .SetPartitionsAssignedHandler(
                (c, partitions) => {
                    TestContext.Current?.OutputWriter.WriteLine(
                        $"Partitions incrementally assigned: [{Join(',', partitions.Select(p => p.Partition.Value))}], all: [{Join(',', c.Assignment.Concat(partitions).Select(p => p.Partition.Value))}]"
                    );
                }
            )
            .SetPartitionsRevokedHandler(
                (c, partitions) => {
                    var remaining = c.Assignment.Where(atp => partitions.All(rtp => rtp.TopicPartition != atp));

                    TestContext.Current?.OutputWriter.WriteLine(
                        $"Partitions incrementally revoked: [{Join(',', partitions.Select(p => p.Partition.Value))}], remaining: [{Join(',', remaining.Select(p => p.Partition.Value))}]"
                    );
                }
            )
            .SetPartitionsLostHandler((_, partitions) => TestContext.Current?.OutputWriter.WriteLine($"Partitions were lost: [{Join(", ", partitions)}]"))
            .Build();
    }
}
