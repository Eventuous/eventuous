using Eventuous.Azure.ServiceBus.Producers;
using Eventuous.Azure.ServiceBus.Subscriptions;

namespace Eventuous.Tests.Azure.ServiceBus;

public class TopicAndQueueSourceAttribute : DataSourceGeneratorAttribute<AzureServiceBusFixture, ServiceBusProducerOptions, ServiceBusSubscriptionOptions> {
    const string QueueName = "queue.1";
    const string TopicName = "topic.1";

    /// <summary>
    /// This is strange. The 'subscription.1' in the emulator has a content type filter. We populate
    /// the content type, but it still gets filtered out. So we use 'subscription.3,' which has no filters.
    /// </summary>
    const string SubscriptionName = "subscription.3";

    readonly ClassDataSourceAttribute<AzureServiceBusFixture> _fixtureDataSource = new() {
        Shared = SharedType.PerTestSession
    };

    public override IEnumerable<Func<(AzureServiceBusFixture, ServiceBusProducerOptions, ServiceBusSubscriptionOptions)>> GenerateDataSources(DataGeneratorMetadata dataGeneratorMetadata) {
        yield return () => {
            var f = _fixtureDataSource.GenerateDataSources(dataGeneratorMetadata).First()();

            return (
                f,
                new() {
                    QueueOrTopicName = QueueName
                },
                new() {
                    QueueOrTopic   = new Queue(QueueName),
                    SubscriptionId = SubscriptionName
                }
            );
        };

        yield return () => {
            var f = _fixtureDataSource.GenerateDataSources(dataGeneratorMetadata).First()();

            return (
                f,
                new() {
                    QueueOrTopicName = TopicName
                },
                new() {
                    QueueOrTopic   = new Topic(TopicName),
                    SubscriptionId = SubscriptionName
                }
            );
        };

        yield return () => {
            var f = _fixtureDataSource.GenerateDataSources(dataGeneratorMetadata).First()();

            return (
                f,
                new() {
                    QueueOrTopicName = TopicName
                },
                new() {
                    QueueOrTopic   = new TopicAndSubscription(TopicName, SubscriptionName),
                    SubscriptionId = "some-subscription" // Random id to show SubscriptionId is not used in this case
                }
            );
        };
    }
}
