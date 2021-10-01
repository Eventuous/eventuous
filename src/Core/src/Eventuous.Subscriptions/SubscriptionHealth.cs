using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eventuous.Subscriptions; 

public class SubscriptionHealthCheck : IHealthCheck {
    readonly IReportHealth[] _subscriptions;
        
    public SubscriptionHealthCheck(IEnumerable<IReportHealth> subscriptions)
        => _subscriptions = subscriptions.ToArray();

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default
    ) {
        var unhealthy = _subscriptions
            .Select(x => (x.SubscriptionId, x.Health))
            .Where(x => !x.Health.IsHealthy)
            .Select(x => (x.SubscriptionId, x.Health.LastException))
            .ToList();
            
        var result = unhealthy.Count > 0
            ? HealthCheckResult.Unhealthy($"Subscriptions dropped: {string.Join(',', unhealthy)}")
            : HealthCheckResult.Healthy();

        return Task.FromResult(result);
    }
}