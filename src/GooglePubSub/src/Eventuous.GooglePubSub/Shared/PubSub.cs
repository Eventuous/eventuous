using Eventuous.Subscriptions.Logging;
using Google.Api.Gax;
using Grpc.Core;

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
        var log         = Logger.Current.InfoLog;

        var publisherServiceApiClient =
            await new PublisherServiceApiClientBuilder { EmulatorDetection = emulatorDetection }
                .BuildAsync(cancellationToken)
                .NoContext();

        log?.Log("Checking topic", topicString);

        try {
            await publisherServiceApiClient.GetTopicAsync(topicName).NoContext();
            log?.Log("Topic exists", topicString);
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            log?.Log("Topic doesn't exist", topicString);
            await publisherServiceApiClient.CreateTopicAsync(topicName).NoContext();
            log?.Log("Created topic", topicString);
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
        var log     = Logger.Current.InfoLog;

        var subscriberServiceApiClient =
            await new SubscriberServiceApiClientBuilder {
                    EmulatorDetection = emulatorDetection
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        log?.Log("Checking subscription for topic", subName, topicName.ToString());

        try {
            await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);
            log?.Log("Subscription exists", subName);
        }
        catch
            (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            log?.Log("Subscription doesn't exist", subName);

            var subscriptionRequest = new Subscription { AckDeadlineSeconds = 60 };

            configureSubscription?.Invoke(subscriptionRequest);
            subscriptionRequest.SubscriptionName = subscriptionName;
            subscriptionRequest.TopicAsTopicName = topicName;

            await subscriberServiceApiClient.CreateSubscriptionAsync(
                    subscriptionRequest
                )
                .NoContext();

            log?.Log("Created subscription", subName);
        }
    }
}
