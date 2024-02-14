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
        services.AddEventuousSqlServer(Container.GetConnectionString(), SchemaName, true);
        services.AddAggregateStore<SqlServerStore>();
        services.AddSingleton(new SqlServerCheckpointStoreOptions { Schema = SchemaName, ConnectionString = Container.GetConnectionString() });
        services.AddSingleton(new TestEventHandlerOptions(null, outputHelper));
        configureServices?.Invoke(services);
    }

    public override async Task<ulong> GetLastPosition() {
        await using var connection = await ConnectionFactory.GetConnection(Container.GetConnectionString(), default);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"select max(global_position) from {SchemaName}.messages";
        var result = await cmd.ExecuteScalarAsync();
        return result is DBNull ? 0 : (ulong)result!;
    }
}

public record SchemaInfo(string Schema);
