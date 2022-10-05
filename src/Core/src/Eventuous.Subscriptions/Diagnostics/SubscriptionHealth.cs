// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eventuous.Subscriptions.Diagnostics;

public interface ISubscriptionHealth {
    void ReportHealthy(string subscriptionId);

    void ReportUnhealthy(string subscriptionId, Exception? exception);
}

public class SubscriptionHealthCheck : ISubscriptionHealth, IHealthCheck {
    readonly Dictionary<string, HealthReport> _healthReports = new();

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken  cancellationToken = default
    ) {
        var unhealthy = _healthReports
            .Select(x => (SubscriptionId: x.Key, Health: x.Value))
            .Where(x => !x.Health.IsHealthy)
            .Select(x => (x.SubscriptionId, x.Health.LastException))
            .ToList();

        var result = unhealthy.Count > 0
            ? HealthCheckResult.Unhealthy($"Subscriptions dropped: {string.Join(',', unhealthy)}")
            : HealthCheckResult.Healthy();

        return Task.FromResult(result);
    }

    public void ReportHealthy(string subscriptionId) 
        => _healthReports[subscriptionId] = HealthReport.Healthy();

    public void ReportUnhealthy(string subscriptionId, Exception? exception)
        => _healthReports[subscriptionId] = HealthReport.Unhealthy(exception);
}