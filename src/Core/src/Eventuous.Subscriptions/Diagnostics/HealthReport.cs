// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Diagnostics;

public record struct HealthReport {
    HealthReport(bool isHealthy, Exception? lastException) {
        IsHealthy     = isHealthy;
        LastException = lastException;
    }

    public static HealthReport Healthy() => new(true, null);
    
    public static HealthReport Unhealthy(Exception? exception) => new(false, exception);

    public bool       IsHealthy     { get; }
    public Exception? LastException { get; }
}