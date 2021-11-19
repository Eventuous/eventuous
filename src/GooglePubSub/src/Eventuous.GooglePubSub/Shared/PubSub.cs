using Google.Api.Gax;
using Grpc.Core;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.GooglePubSub.Shared;

public static class PubSub {
    public static EmulatorDetection DetectEmulator(this SubscriberClient.ClientCreationSettings? value)
        => value?.EmulatorDetection ?? EmulatorDetection.None;

    public static EmulatorDetection DetectEmulator(this PublisherClient.ClientCreationSettings? value)
        => value?.EmulatorDetection ?? EmulatorDetection.None;

    public static async Task CreateTopic(
        TopicName         topicName,
        EmulatorDetection emulatorDetection,
        CancellationToken cancellationToken
    ) {
        var topicString = topicName.ToString();

        var publisherServiceApiClient =
            await new PublisherServiceApiClientBuilder {
                    EmulatorDetection = emulatorDetection
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        Log.Info("Checking topic", topicString);

        try {
            await publisherServiceApiClient.GetTopicAsync(topicName).NoContext();
            Log.Info("Topic exists", topicString);
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            Log.Info("Topic doesn't exist", topicString);
            await publisherServiceApiClient.CreateTopicAsync(topicName).NoContext();
            Log.Info("Created topic", topicString);
        }
    }

    public static async Task CreateSubscription(
        SubscriptionName      subscriptionName,
        TopicName             topicName,
        Action<Subscription>? configureSubscription,
        EmulatorDetection     emulatorDetection,
        CancellationToken     cancellationToken
    ) {
        var subName = subscriptionName.ToString();
        var subscriberServiceApiClient =
            await new SubscriberServiceApiClientBuilder {
                    EmulatorDetection = emulatorDetection
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        Log.Info("Checking subscription for topic", subName, topicName.ToString());

        try {
            await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);
            Log.Info("Subscription exists", subName);
        }
        catch
            (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            Log.Info("Subscription doesn't exist", subName);

            var subscriptionRequest = new Subscription { AckDeadlineSeconds = 60 };

            configureSubscription?.Invoke(subscriptionRequest);
            subscriptionRequest.SubscriptionName = subscriptionName;
            subscriptionRequest.TopicAsTopicName = topicName;

            await subscriberServiceApiClient.CreateSubscriptionAsync(
                    subscriptionRequest
                )
                .NoContext();

            Log.Info("Created subscription", subName);
        }
    }
}