using Eventuous.Tests.OpenTelemetry;

namespace Eventuous.Tests.EventStore.Metrics;

[ClassDataSource<MetricsFixture>]
[InheritsTests]
public class MetricsTests(MetricsFixture fixture) : MetricsTestsBase(fixture);
