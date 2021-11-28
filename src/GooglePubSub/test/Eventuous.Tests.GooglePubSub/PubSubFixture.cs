namespace Eventuous.Tests.GooglePubSub;

public static class PubSubFixture {
    public const string ProjectId = "master-works-208819";

    //Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID");

    static PubSubFixture() {
        Environment.SetEnvironmentVariable(
            "GOOGLE_APPLICATION_CREDENTIALS",
            "/Users/alexey/google-test-creds.json"
        );
    }

    public static async Task DeleteSubscription(string subscriptionId) {
        var subscriber       = await SubscriberServiceApiClient.CreateAsync();
        var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);
        await subscriber.DeleteSubscriptionAsync(subscriptionName);
    }

    public static async Task DeleteTopic(string topicId) {
        var publisher = await PublisherServiceApiClient.CreateAsync();
        var topicName = TopicName.FromProjectTopic(ProjectId, topicId);
        await publisher.DeleteTopicAsync(topicName);
    }
}