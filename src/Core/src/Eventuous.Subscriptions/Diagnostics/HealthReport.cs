namespace Eventuous.Subscriptions.Diagnostics;

public record HealthReport {
    HealthReport(bool isHealthy, Exception? lastException) {
        IsHealthy     = isHealthy;
        LastException = lastException;
    }

    public static HealthReport Healthy() => new(true, null);
    
    public static HealthReport Unhealthy(Exception? exception) => new(false, exception);

    public bool       IsHealthy     { get; }
    public Exception? LastException { get; }
}