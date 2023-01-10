namespace Eventuous.Tests.GooglePubSub;

/// <summary>
/// This test is manual as it requires at least a minute to run, so we are using a subscription, which already exists
/// </summary>
public class Monitoring {

    public Monitoring() {
        // Keep this for queries to document
        // _undeliveredCountRequest = new ListTimeSeriesRequest {
        //     Name = $"projects/{PubSubFixture.ProjectId}",
        //     Filter = "metric.type = \"pubsub.googleapis.com/subscription/num_undelivered_messages\" "
        //            + $"AND resource.label.subscription_id = \"{subId}\""
        // };
        //
        // _oldestAgeRequest = new ListTimeSeriesRequest {
        //     Name = $"projects/{PubSubFixture.ProjectId}",
        //     Filter = "metric.type = \"pubsub.googleapis.com/subscription/oldest_unacked_message_age\" "
        //            + $"AND resource.label.subscription_id = \"{subId}\""
        // };
    }
}