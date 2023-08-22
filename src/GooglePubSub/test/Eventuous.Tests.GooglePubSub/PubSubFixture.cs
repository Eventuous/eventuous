using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Google.Api.Gax;

namespace Eventuous.Tests.GooglePubSub;

public class PubSubFixture : IAsyncLifetime {
    public static string PubsubProjectId => "test-id";

    public static async Task DeleteSubscription(string subscriptionId) {
        var builder          = new SubscriberServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly };
        var subscriber       = await builder.BuildAsync();
        var subscriptionName = SubscriptionName.FromProjectSubscription(PubsubProjectId, subscriptionId);
        await subscriber.DeleteSubscriptionAsync(subscriptionName);
    }

    public static async Task DeleteTopic(string topicId) {
        var builder   = new PublisherServiceApiClientBuilder { EmulatorDetection = EmulatorDetection.EmulatorOnly };
        var publisher = await builder.BuildAsync();
        var topicName = TopicName.FromProjectTopic(PubsubProjectId, topicId);
        await publisher.DeleteTopicAsync(topicName);
    }

    IContainer _container = null!;

    public async Task InitializeAsync() {
        const int port = 8085;

        _container = new ContainerBuilder()
            .WithImage("gcr.io/google.com/cloudsdktool/google-cloud-cli:emulators")
            .WithPortBinding(port, true)
            .WithEntrypoint("gcloud")
            .WithCommand("beta", "emulators", "pubsub", "start", $"--host-port=0.0.0.0:{port}", $"--project={PubsubProjectId}")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("(?s).*started.*$"))
            .Build();
        await _container.StartAsync();
        Environment.SetEnvironmentVariable("PUBSUB_EMULATOR_HOST", $"{_container.Hostname}:{_container.GetMappedPublicPort(port)}");
        Environment.SetEnvironmentVariable("PUBSUB_PROJECT_ID", PubsubProjectId);
    }

    public async Task DisposeAsync() {
        await _container.StopAsync();
    }
}
