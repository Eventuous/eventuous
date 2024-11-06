using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Tests.SqlServer.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscriptionFixture<TSubscription, TSubscriptionOptions, TEventHandler>(
        Action<TSubscriptionOptions> configureOptions,
        bool                         autoStart         = true,
        Action<IServiceCollection>?  configureServices = null,
        LogLevel                     logLevel          = LogLevel.Debug
    )
    : SubscriptionFixtureBase<SqlEdgeContainer, TSubscription, TSubscriptionOptions, SqlServerCheckpointStore, TEventHandler>(
        autoStart,
        logLevel
    )
    where TSubscription : SqlServerSubscriptionBase<TSubscriptionOptions>
    where TSubscriptionOptions : SqlServerSubscriptionBaseOptions
    where TEventHandler : class, IEventHandler {
    protected internal readonly string SchemaName = GetSchemaName();

    protected override SqlEdgeContainer CreateContainer() => SqlContainer.Create();

    protected override SqlServerCheckpointStore GetCheckpointStore(IServiceProvider sp)
        => sp.GetRequiredService<SqlServerCheckpointStore>();

    protected override void ConfigureSubscription(TSubscriptionOptions options) {
        options.Schema           = SchemaName;
        options.ConnectionString = Container.GetConnectionString();
        configureOptions(options);
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddSingleton(new SchemaInfo(SchemaName));
        services.AddEventuousSqlServer(Container.GetConnectionString(), SchemaName, true);
        services.AddEventStore<SqlServerStore>();
        services.AddSqlServerCheckpointStore();
        services.AddSingleton(new TestEventHandlerOptions());
        configureServices?.Invoke(services);
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        ConnectionOptions = provider.GetRequiredService<SqlServerConnectionOptions>();

        if (ConnectionOptions is null) {
            throw new InvalidOperationException("Subscription options not found");
        }

        ConnectionString = ConnectionOptions.ConnectionString ?? throw new InvalidOperationException("Connection string not found");
    }

    public override async Task<ulong> GetLastPosition() {
        await using var connection = await ConnectionFactory.GetConnection(Container.GetConnectionString(), default);
        await using var cmd        = connection.CreateCommand();
        cmd.CommandText = $"select max(GlobalPosition) from {SchemaName}.messages";
        var result = await cmd.ExecuteScalarAsync();

        return (ulong)(result is DBNull ? 0 : (long)result!);
    }

    SqlServerConnectionOptions ConnectionOptions { get; set; } = null!;

    public string ConnectionString { get; private set; } = null!;
}

public record SchemaInfo(string Schema);