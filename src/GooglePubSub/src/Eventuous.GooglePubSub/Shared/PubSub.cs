// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Logging;
using Google.Api.Gax;
using Grpc.Core;

namespace Eventuous.GooglePubSub.Shared;

public static class PubSub {
    [PublicAPI]
    public static EmulatorDetection DetectEmulator(this SubscriberClient.ClientCreationSettings? value) => value?.EmulatorDetection ?? EmulatorDetection.None;

    [PublicAPI]
    public static EmulatorDetection DetectEmulator(this PublisherClient.ClientCreationSettings? value) => value?.EmulatorDetection ?? EmulatorDetection.None;

    public static async Task CreateTopic(
            TopicName              topicName,
            EmulatorDetection      emulatorDetection,
            Action<string, string> log,
            CancellationToken      cancellationToken
        ) {
        var topicString = topicName.ToString();

        var publisherServiceApiClient =
            await new PublisherServiceApiClientBuilder { EmulatorDetection = emulatorDetection }.BuildAsync(cancellationToken).NoContext();

        Log("Checking topic");

        try {
            await publisherServiceApiClient.GetTopicAsync(topicName).NoContext();
            Log("Topic exists");
        } catch (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            Log("Topic doesn't exist");
            await publisherServiceApiClient.CreateTopicAsync(topicName).NoContext();
            Log("Created topic");
        }

        return;

        void Log(string message) => log($"{message}: {{Topic}}", topicString!);
    }

    public static async Task CreateSubscription(
            SubscriptionName      subscriptionName,
            TopicName             topicName,
            Action<Subscription>? configureSubscription,
            EmulatorDetection     emulatorDetection,
            CancellationToken     cancellationToken
        ) {
        var subName = subscriptionName.ToString()!;
        var log     = Logger.Current.InfoLog;

        var subscriberServiceApiClient = await new SubscriberServiceApiClientBuilder { EmulatorDetection = emulatorDetection }
            .BuildAsync(cancellationToken)
            .NoContext();

        log?.Log("Checking subscription for topic", subName, topicName.ToString());

        try {
            await subscriberServiceApiClient.GetSubscriptionAsync(subscriptionName);
            log?.Log("Subscription exists", subName);
        } catch (RpcException e) when (e.Status.StatusCode == StatusCode.NotFound) {
            log?.Log("Subscription doesn't exist", subName);

            var subscriptionRequest = new Subscription { AckDeadlineSeconds = 60 };

            configureSubscription?.Invoke(subscriptionRequest);
            subscriptionRequest.SubscriptionName = subscriptionName;
            subscriptionRequest.TopicAsTopicName = topicName;

            await subscriberServiceApiClient.CreateSubscriptionAsync(subscriptionRequest).NoContext();

            log?.Log("Created subscription", subName);
        }
    }
}
