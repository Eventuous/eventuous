using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eventuous.Subscriptions;
using Google.Cloud.Monitoring.V3;
using Google.Protobuf.WellKnownTypes;
using Xunit;

namespace Eventuous.Tests.GooglePubSub {
    /// <summary>
    /// This test is manual as it requires at least a minute to run, so we are using a subscription, which already exists
    /// </summary>
    public class Monitoring {
        readonly MetricServiceClient   _metricClient;
        readonly ListTimeSeriesRequest _undeliveredCountRequest;
        readonly ListTimeSeriesRequest _oldestAgeRequest;

        public Monitoring() {
            _metricClient = MetricServiceClient.Create();

            const string subId = "test-7ffc718448eb41478252677b6889332f";

            _undeliveredCountRequest = new ListTimeSeriesRequest {
                Name = $"projects/{PubSubFixture.ProjectId}",
                Filter = "metric.type = \"pubsub.googleapis.com/subscription/num_undelivered_messages\" "
                       + $"AND resource.label.subscription_id = \"{subId}\""
            };

            _oldestAgeRequest = new ListTimeSeriesRequest {
                Name = $"projects/{PubSubFixture.ProjectId}",
                Filter = "metric.type = \"pubsub.googleapis.com/subscription/oldest_unacked_message_age\" "
                       + $"AND resource.label.subscription_id = \"{subId}\""
            };
        }

        [Fact]
        public async Task Should_get_subscription_metrics() {
            await GetLastEventPosition(CancellationToken.None);
        }

        async Task<EventPosition> GetLastEventPosition(CancellationToken cancellationToken) {
            var interval = new TimeInterval {
                StartTime = Timestamp.FromDateTime(DateTime.UtcNow - TimeSpan.FromMinutes(2)),
                EndTime   = Timestamp.FromDateTime(DateTime.UtcNow)
            };

            var undelivered = await GetPoint(_undeliveredCountRequest);
            var oldestAge = await GetPoint(_oldestAgeRequest);
            var age = oldestAge == null ? DateTime.UtcNow : DateTime.UtcNow.AddSeconds(-oldestAge.Value.Int64Value);

            return new EventPosition((ulong?) undelivered?.Value?.Int64Value, age);

            async Task<Point?> GetPoint(ListTimeSeriesRequest request) {
                request.Interval = interval;

                var result = _metricClient.ListTimeSeriesAsync(request);
                var page   = await result.ReadPageAsync(1, cancellationToken);
                return page.FirstOrDefault()?.Points?.FirstOrDefault();
            }
        }
    }
}