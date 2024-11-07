using Eventuous.Tests.OpenTelemetry;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Metrics;

[ClassDataSource<MetricsFixture>]
[InheritsTests]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture);
