using Google.Api.Gax;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace Eventuous.GooglePubSub.Shared;

public static class PubSub {
    public static EmulatorDetection DetectEmulator(this SubscriberClient.ClientCreationSettings? value)
        => value?.EmulatorDetection ?? EmulatorDetection.None;

    public static EmulatorDetection DetectEmulator(this PublisherClient.ClientCreationSettings? value)
        => value?.EmulatorDetection ?? EmulatorDetection.None;

    public static async Task CreateTopic(
        TopicName         topicName,
        EmulatorDetection emulatorDetection,
        ILogger?          log,
        CancellationToken cancellationToken
    ) {
        var publisherServiceApiClient =
            await new PublisherServiceApiClientBuilder {
                    EmulatorDetection = emulatorDetection
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        log?.LogInformation("Checking topic {Topic}", topicName);

        try {
            await publisherServiceApiClient.GetTopicAsync(topicName).NoContext();
            log?.LogInformation("Topic {Topic} exists", topicName);
        }
        catch (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            log?.LogInformation("Topic {Topic} doesn't exist", topicName);
            await publisherServiceApiClient.CreateTopicAsync(topicName).NoContext();
            log?.LogInformation("Created topic {Topic}", topicName);
        }
    }

    public static async Task CreateSubscription(
        SubscriptionName      subscriptionName,
        TopicName             topicName,
        Action<Subscription>? configureSubscription,
        EmulatorDetection     emulatorDetection,
        ILogger?              log,
        CancellationToken     cancellationToken
    ) {
        var subscriberServiceApiClient =
            await new SubscriberServiceApiClientBuilder {
                    EmulatorDetection = emulatorDetection
                }
                .BuildAsync(cancellationToken)
                .NoContext();

        log?.LogInformation(
            "Checking subscription {Subscription} for {Topic}",
            subscriptionName,
            topicName
        );

        try {
            await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);

            log?.LogInformation(
                "Subscription {Subscription} for {Topic} exists",
                subscriptionName,
                topicName
            );
        }
        catch
            (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            log?.LogInformation(
                "Subscription {Subscription} for {Topic} doesn't exist",
                subscriptionName,
                topicName
            );

            var subscriptionRequest = new Subscription { AckDeadlineSeconds = 60 };

            configureSubscription?.Invoke(subscriptionRequest);
            subscriptionRequest.SubscriptionName = subscriptionName;
            subscriptionRequest.TopicAsTopicName = topicName;

            await subscriberServiceApiClient.CreateSubscriptionAsync(
                    subscriptionRequest
                )
                .NoContext();

            log?.LogInformation(
                "Created subscription {Subscription} for {Topic}",
                subscriptionName,
                topicName
            );
        }
    }
}