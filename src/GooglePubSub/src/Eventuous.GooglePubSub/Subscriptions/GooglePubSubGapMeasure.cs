using Eventuous.Subscriptions.Diagnostics;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;

namespace Eventuous.GooglePubSub.Subscriptions;

[PublicAPI]
public class GooglePubSubGapMeasure : ISubscriptionGapMeasure {
    const string PubSubMetricUndeliveredMessagesCount = "num_undelivered_messages";
    const string PubSubMetricOldestUnackedMessageAge  = "oldest_unacked_message_age";

    ListTimeSeriesRequest _undeliveredCountRequest = null!;
    ListTimeSeriesRequest _oldestAgeRequest        = null!;
    MetricServiceClient   _metricClient            = null!;
    bool                  _monitoringEnabled;

    public GooglePubSubGapMeasure(PubSubSubscriptionOptions options) {
        _undeliveredCountRequest = GetFilteredRequest(PubSubMetricUndeliveredMessagesCount);
        _oldestAgeRequest        = GetFilteredRequest(PubSubMetricOldestUnackedMessageAge);

        var emulationEnabled =
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PUBSUB_EMULATOR_HOST"));

        _monitoringEnabled = !emulationEnabled && options.EnableMonitoring;

        if (_monitoringEnabled)
            _metricClient = MetricServiceClient.Create();

        ListTimeSeriesRequest GetFilteredRequest(string metric)
            => new() {
                Name = $"projects/{options.ProjectId}",
                Filter = $"metric.type = \"pubsub.googleapis.com/subscription/{metric}\" "
                       + $"AND resource.label.subscription_id = \"{options.SubscriptionId}\""
            };
    }

    public async Task<SubscriptionGap> GetSubscriptionGap(CancellationToken cancellationToken) {
        if (!_monitoringEnabled) return new SubscriptionGap(0, TimeSpan.Zero);

        var now = DateTime.UtcNow;

        // Subscription metrics are sampled each 60 sec, so we need to use an extended period
        var interval = new TimeInterval {
            StartTime = Timestamp.FromDateTime(now - TimeSpan.FromMinutes(2)),
            EndTime   = Timestamp.FromDateTime(now)
        };

        var undelivered = await GetPoint(_undeliveredCountRequest).NoContext();
        var oldestAge   = await GetPoint(_oldestAgeRequest).NoContext();

        var age = oldestAge == null
            ? TimeSpan.Zero
            : TimeSpan.FromSeconds(oldestAge.Value.Int64Value);

        return new SubscriptionGap((ulong)(undelivered?.Value?.Int64Value ?? 0), age);

        async Task<Point?> GetPoint(ListTimeSeriesRequest request) {
            request.Interval = interval;

            var result = _metricClient.ListTimeSeriesAsync(request);
            var page   = await result.ReadPageAsync(1, cancellationToken).NoContext();
            return page.FirstOrDefault()?.Points?.FirstOrDefault();
        }
    }
}