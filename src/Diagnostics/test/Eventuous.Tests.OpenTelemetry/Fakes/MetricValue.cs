namespace Eventuous.Tests.OpenTelemetry.Fakes;

public record MetricValue(string Name, string[] Keys, object[] Values, double Value);
