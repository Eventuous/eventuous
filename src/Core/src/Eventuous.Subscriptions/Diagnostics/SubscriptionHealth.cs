// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Eventuous.Subscriptions.Diagnostics;

public interface ISubscriptionHealth {
    void ReportHealthy(string subscriptionId);

    void ReportUnhealthy(string subscriptionId, Exception? exception);
}

public class SubscriptionHealthCheck : ISubscriptionHealth, IHealthCheck {
    readonly Dictionary<string, HealthReport> _healthReports = new();

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default) {
        var        unhealthy  = new List<string>();
        var        data       = new Dictionary<string, object>();
        var        allHealthy = true;
        Exception? exception  = null;

        foreach (var report in _healthReports) {
            data[report.Key] = report.Value.IsHealthy ? "Healthy" : "Unhealthy";

            if (report.Value.IsHealthy) continue;

            unhealthy.Add(report.Key);
            allHealthy = false;
            exception  = report.Value.LastException;
        }

        var result = !allHealthy
            ? HealthCheckResult.Unhealthy($"Subscriptions dropped: {string.Join(',', unhealthy)}", exception, data)
            : HealthCheckResult.Healthy("All subscriptions are healthy", data);

        return Task.FromResult(result);
    }

    public void ReportHealthy(string subscriptionId) => _healthReports[subscriptionId] = HealthReport.Healthy();

    public void ReportUnhealthy(string subscriptionId, Exception? exception) => _healthReports[subscriptionId] = HealthReport.Unhealthy(exception);
}
