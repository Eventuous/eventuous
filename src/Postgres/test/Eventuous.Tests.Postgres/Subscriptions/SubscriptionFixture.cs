using Bogus;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Subscriptions;
using Eventuous.Tests.Postgres.Fixtures;
using Eventuous.Tests.Subscriptions.Base;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Subscriptions;

public class SubscriptionFixture<TSubscription, TSubscriptionOptions, TEventHandler>(
        Action<TSubscriptionOptions> configureOptions,
        ITestOutputHelper            outputHelper,
        bool                         autoStart         = true,
        Action<IServiceCollection>?  configureServices = null,
        LogLevel                     logLevel          = LogLevel.Debug
    )
    : SubscriptionFixtureBase<PostgreSqlContainer, TSubscription, TSubscriptionOptions, PostgresCheckpointStore, TEventHandler>(
        outputHelper,
        autoStart,
        logLevel
    )
    where TSubscription : PostgresSubscriptionBase<TSubscriptionOptions>
    where TSubscriptionOptions : PostgresSubscriptionBaseOptions
    where TEventHandler : class, IEventHandler {
    protected internal readonly string SchemaName = new Faker().Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ITestOutputHelper _outputHelper = outputHelper;

    protected override PostgreSqlContainer CreateContainer() => PostgresContainer.Create();

    protected override PostgresCheckpointStore GetCheckpointStore(IServiceProvider sp)
        => new(sp.GetRequiredService<NpgsqlDataSource>(), SchemaName, sp.GetService<ILoggerFactory>());

    protected override void ConfigureSubscription(TSubscriptionOptions options) {
        options.Schema = SchemaName;
        configureOptions(options);
    }

    protected override void SetupServices(IServiceCollection services) {
        base.SetupServices(services);
        services.AddSingleton(new SchemaInfo(SchemaName));
        services.AddEventuousPostgres(Container.GetConnectionString(), SchemaName, true);
        services.AddAggregateStore<PostgresStore>();
        services.AddSingleton(new TestEventHandlerOptions(null, _outputHelper));
        configureServices?.Invoke(services);
    }

    protected override void GetDependencies(IServiceProvider provider) {
        base.GetDependencies(provider);
        DataSource = provider.GetRequiredService<NpgsqlDataSource>();
    }

    public override async Task<ulong> GetLastPosition() {
        var             query      = $"select m.global_position from {SchemaName}.messages m order by m.global_position desc limit 1";
        await using var connection = await DataSource.OpenConnectionAsync();
        await using var cmd        = new NpgsqlCommand(query, connection);
        await using var reader     = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return reader.IsDBNull(0) ? 0 : (ulong)reader.GetInt64(0);
    }

    public NpgsqlDataSource DataSource { get; private set; } = null!;
}

public record SchemaInfo(string Schema);
