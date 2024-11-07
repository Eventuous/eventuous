// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Diagnostics;

readonly record struct HealthReport {
    HealthReport(bool isHealthy, Exception? lastException) {
        IsHealthy     = isHealthy;
        LastException = lastException;
    }

    public static HealthReport Healthy() => new(true, null);
    
    public static HealthReport Unhealthy(Exception? exception) => new(false, exception);

    public bool       IsHealthy     { get; }
    public Exception? LastException { get; }
}