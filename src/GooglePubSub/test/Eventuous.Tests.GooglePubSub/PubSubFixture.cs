using Microsoft.Extensions.Configuration;

namespace Eventuous.Tests.GooglePubSub;

public static class PubSubFixture {
    const string Credentials  = "GOOGLE_APPLICATION_CREDENTIALS";
    const string ProjectId    = "PUBSUB_PROJECT_ID";
    const string SettingsFile = "appSettings.json";

    static PubSubFixture() {
        if (File.Exists(SettingsFile)) {
            var config = new ConfigurationBuilder().AddJsonFile(SettingsFile).Build();
            Environment.SetEnvironmentVariable(Credentials, config["Credentials"]);
            Environment.SetEnvironmentVariable(ProjectId, config["ProjectId"]);
        }

        var existingCredentials = Environment.GetEnvironmentVariable(Credentials);
        var existingProject     = Environment.GetEnvironmentVariable(ProjectId);
        if (existingCredentials == null || existingProject == null) {
            throw new Exception(
                $"Environment variables {Credentials} and {ProjectId} must be set to run the tests"
            );
        }

        PubsubProjectId = existingProject;
    }

    public static string PubsubProjectId { get; }

    public static async Task DeleteSubscription(string subscriptionId) {
        var subscriber       = await SubscriberServiceApiClient.CreateAsync();
        var subscriptionName = SubscriptionName.FromProjectSubscription(PubsubProjectId, subscriptionId);
        await subscriber.DeleteSubscriptionAsync(subscriptionName);
    }

    public static async Task DeleteTopic(string topicId) {
        var publisher = await PublisherServiceApiClient.CreateAsync();
        var topicName = TopicName.FromProjectTopic(PubsubProjectId, topicId);
        await publisher.DeleteTopicAsync(topicName);
    }
}
