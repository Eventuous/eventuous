namespace Eventuous.Subscriptions;

public interface IReportHealth {
    string       ServiceId    { get; }
    HealthReport HealthReport { get; }
}

public record HealthReport(bool IsHealthy, Exception? LastException);