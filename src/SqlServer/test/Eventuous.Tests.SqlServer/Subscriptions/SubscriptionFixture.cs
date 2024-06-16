using Bogus;
using Eventuous.SqlServer;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Tests.SqlServer.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscriptionFixture<TSubscription, TSubscriptionOptions, TEventHandler>(
        Action<TSubscriptionOptions> configureOptions,
        ITestOutputHelper            outputHelper,
        bool                         autoStart         = true,
        Action<IServiceCollection>?  configureServices = null,
        LogLevel                     logLevel          = LogLevel.Debug
    )
    : SubscriptionFixtureBase<SqlEdgeContainer, TSubscription, TSubscriptionOptions, SqlServerCheckpointStore, TEventHandler>(
        outputHelper,
        autoStart,
        logLevel
    )
    where TSubscription : SqlServerSubscriptionBase<TSubscriptionOptions>
    where TSubscriptionOptions : SqlServerSubscriptionBaseOptions
    where TEventHandler : class, IEventHandler {
    protected internal readonly string SchemaName = new Faker().Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ITestOutputHelper _outputHelper = outputHelper;

    protected override SqlEdgeContainer CreateContainer() => SqlContainer.Create();

    protected override SqlServerCheckpointStore GetCheckpointStore(IServiceProvider sp)
        => new(sp.GetRequiredService<SqlServerCheckpointStoreOptions>());

    protected override void ConfigureSubscription(TSubscriptionOptions options) {
        options.Schema           = SchemaName;
        options.ConnectionString = Container.GetConnectionString();
        configureOptions(options);
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddSingleton(new SchemaInfo(SchemaName));
        services.AddSingleton(new SubscriptionOptions { ConnectionString = Container.GetConnectionString() });
        services.AddEventuousSqlServer(Container.GetConnectionString(), SchemaName, true);
        services.AddAggregateStore<SqlServerStore>();
        services.AddSingleton(new SqlServerCheckpointStoreOptions { Schema = SchemaName, ConnectionString = Container.GetConnectionString() });
        services.AddSingleton(new TestEventHandlerOptions(null, _outputHelper));
        configureServices?.Invoke(services);
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        SubscriptionOptions = provider.GetRequiredService<SubscriptionOptions>();

        if (SubscriptionOptions is null) {
            throw new InvalidOperationException("Subscription options not found");
        }

        ConnectionString = SubscriptionOptions.ConnectionString ?? throw new InvalidOperationException("Connection string not found");
    }

    public override async Task<ulong> GetLastPosition() {
        await using var connection = await ConnectionFactory.GetConnection(Container.GetConnectionString(), default);
        await using var cmd        = connection.CreateCommand();
        cmd.CommandText = $"select max(GlobalPosition) from {SchemaName}.messages";
        var result = await cmd.ExecuteScalarAsync();

        return (ulong)(result is DBNull ? 0 : (long)result!);
    }

    public SubscriptionOptions SubscriptionOptions { get; private set; } = null!;

    public string ConnectionString { get; private set; } = null!;
}

public record SchemaInfo(string Schema);

public record SubscriptionOptions : SqlServerSubscriptionBaseOptions;
