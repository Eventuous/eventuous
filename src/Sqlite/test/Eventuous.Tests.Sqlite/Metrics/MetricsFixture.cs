using Eventuous.Diagnostics;
using Eventuous.Diagnostics.OpenTelemetry;
using Eventuous.Producers;
using Eventuous.Sql.Base.Producers;
using Eventuous.Sqlite;
using Eventuous.Sqlite.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Registrations;
using Eventuous.TestHelpers.TUnit.Logging;
using Eventuous.Tests.OpenTelemetry.Fakes;
using Eventuous.Tests.OpenTelemetry.Fixtures;
using Eventuous.Tests.Sqlite.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Eventuous.Tests.Sqlite.Metrics;

/// <summary>
/// SQLite counterpart of <see cref="MetricsSubscriptionFixtureBase{TContainer,TProducer,TSubscription,TSubscriptionOptions}"/>,
/// built on the container-less <see cref="SqliteStoreFixtureBase"/>.
/// </summary>
public class MetricsFixture : SqliteStoreFixtureBase, IMetricsSubscriptionFixtureBase {
    static readonly KeyValuePair<string, string> DefaultTag = new("test", "foo");

    readonly string _schemaName = GetSchemaName();

    static MetricsFixture() => EventuousDiagnostics.AddDefaultTag(DefaultTag.Key, DefaultTag.Value);

    public MetricsFixture() => TypeMapper.RegisterKnownEventTypes(typeof(TestEvent).Assembly);

    public int            Count           => 100;
    public StreamName     Stream          { get; }              = new($"test-{Guid.NewGuid():N}");
    public string         DefaultTagKey   => DefaultTag.Key;
    public string         DefaultTagValue => DefaultTag.Value;
    public string         SubscriptionId  => "test-sub";
    public IProducer      Producer        { get; private set; } = null!;
    public MessageCounter Counter         { get; private set; } = null!;
    public TestExporter   Exporter        { get; }              = new();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlite(ConnectionString, _schemaName, true);
        services.AddEventStore<SqliteStore>();
        services.AddProducer<UniversalProducer>();
        services.AddSingleton<MessageCounter>();

        services.AddSubscription<SqliteStreamSubscription, SqliteStreamSubscriptionOptions>(
            SubscriptionId,
            builder => builder
                .Configure(
                    options => {
                        options.Schema           = _schemaName;
                        options.ConnectionString = ConnectionString;
                        options.Stream           = Stream;
                    }
                )
                .UseCheckpointStore<NoOpCheckpointStore>()
                .AddEventHandler<TestHandler>()
        );

        services.AddOpenTelemetry().WithMetrics(builder => builder.AddEventuousSubscriptions().AddReader(new BaseExportingMetricReader(Exporter)));
    }

    protected override void GetDependencies(IServiceProvider provider) {
        provider.AddEventuousLogs();
        Producer = provider.GetRequiredService<UniversalProducer>();
        Counter  = provider.GetRequiredService<MessageCounter>();
    }

    public override async ValueTask DisposeAsync() {
        await base.DisposeAsync();
        Exporter.Dispose();
    }
}
