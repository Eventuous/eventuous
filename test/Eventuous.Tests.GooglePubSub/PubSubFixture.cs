using System;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Grpc.Core;

namespace Eventuous.Tests.GooglePubSub {
    public static class PubSubFixture {
        public static readonly string ProjectId = "master-works-208819";
            //Environment.GetEnvironmentVariable("PUBSUB_PROJECT_ID");

        static PubSubFixture() {
            Environment.SetEnvironmentVariable(
                "GOOGLE_APPLICATION_CREDENTIALS",
                "/Users/alexey/google-test-creds.json"
            );
        }

        public static async Task CreateTopic(string topicId) {
            var publisher = await PublisherServiceApiClient.CreateAsync();
            var topicName = TopicName.FromProjectTopic(ProjectId, topicId);

            try {
                await publisher.CreateTopicAsync(topicName);
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) { }
        }

        public static async Task CreateSubscription(string topicId, string subscriptionId) {
            var subscriber       = await SubscriberServiceApiClient.CreateAsync();
            var topicName        = TopicName.FromProjectTopic(ProjectId, topicId);
            var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);

            try {
                await subscriber.CreateSubscriptionAsync(
                    subscriptionName,
                    topicName,
                    pushConfig: null,
                    ackDeadlineSeconds: 60
                );
            }
            catch (RpcException e) when (e.Status.StatusCode == StatusCode.AlreadyExists) {
                // Already exists.  That's fine.
            }
        }

        public static async Task DeleteSubscription(string subscriptionId) {
            var subscriber       = await SubscriberServiceApiClient.CreateAsync();
            var subscriptionName = SubscriptionName.FromProjectSubscription(ProjectId, subscriptionId);
            await subscriber.DeleteSubscriptionAsync(subscriptionName);
        }

        public static async Task DeleteTopic(string topicId) {
            var   publisher = await PublisherServiceApiClient.CreateAsync();
            var   topicName = TopicName.FromProjectTopic(ProjectId, topicId);
            await publisher.DeleteTopicAsync(topicName);
        }
    }
}