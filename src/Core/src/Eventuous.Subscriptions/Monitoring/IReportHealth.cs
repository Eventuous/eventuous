namespace Eventuous.Subscriptions.Monitoring;

public interface IReportHealth {
    string       ServiceId    { get; }
    HealthReport HealthReport { get; }
}

public record HealthReport(bool IsHealthy, Exception? LastException);