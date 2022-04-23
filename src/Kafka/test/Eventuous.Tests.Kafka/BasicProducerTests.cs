using Confluent.Kafka;

namespace Eventuous.Tests.Kafka;

public class BasicProducerTests {
    public async Task ShouldProduceAndWait() {
        var brokerList = "localhost:9092";

        var config = new ConsumerConfig {
            BootstrapServers            = brokerList,
            GroupId                     = "csharp-consumer",
            EnableAutoCommit            = false,
            StatisticsIntervalMs        = 5000,
            SessionTimeoutMs            = 6000,
            AutoOffsetReset             = AutoOffsetReset.Earliest,
            EnablePartitionEof          = true,
            PartitionAssignmentStrategy = PartitionAssignmentStrategy.CooperativeSticky
        };

        const int commitPeriod = 5;

        using var consumer = new ConsumerBuilder<Ignore, string>(config)
            .SetErrorHandler((_,      e) => Console.WriteLine($"Error: {e.Reason}"))
            .SetStatisticsHandler((_, json) => Console.WriteLine($"Statistics: {json}"))
            .SetPartitionsAssignedHandler(
                (c, partitions) => {
                    // Since a cooperative assignor (CooperativeSticky) has been configured, the
                    // partition assignment is incremental (adds partitions to any existing assignment).
                    Console.WriteLine(
                        "Partitions incrementally assigned: ["                                           +
                        string.Join(',', partitions.Select(p => p.Partition.Value))                      +
                        "], all: ["                                                                      +
                        string.Join(',', c.Assignment.Concat(partitions).Select(p => p.Partition.Value)) +
                        "]"
                    );

                    // Possibly manually specify start offsets by returning a list of topic/partition/offsets
                    // to assign to, e.g.:
                    // return partitions.Select(tp => new TopicPartitionOffset(tp, externalOffsets[tp]));
                }
            )
            .SetPartitionsRevokedHandler(
                (c, partitions) => {
                    // Since a cooperative assignor (CooperativeSticky) has been configured, the revoked
                    // assignment is incremental (may remove only some partitions of the current assignment).
                    var remaining = c.Assignment.Where(
                        atp => partitions.All(rtp => rtp.TopicPartition != atp)
                    );

                    Console.WriteLine(
                        "Partitions incrementally revoked: ["                       +
                        string.Join(',', partitions.Select(p => p.Partition.Value)) +
                        "], remaining: ["                                           +
                        string.Join(',', remaining.Select(p => p.Partition.Value))  +
                        "]"
                    );
                }
            )
            .SetPartitionsLostHandler(
                (c, partitions) => {
                    Console.WriteLine($"Partitions were lost: [{string.Join(", ", partitions)}]");
                }
            )
            .Build();
    }
}
