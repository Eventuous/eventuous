using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.Postgresql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventStore      EventStore     { get; private set; } = null!;
    public IAggregateStore  AggregateStore { get; private set; } = null!;
    public NpgsqlDataSource DataSource     { get; private set; } = null!;

    readonly ActivityListener _listener = DummyActivityListener.Create();
    ServiceProvider           _provider = null!;

    public string SchemaName { get; } = new Faker().Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    IEventSerializer Serializer { get; } =
        new DefaultEventSerializer(new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));

    readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithUsername("postgres")
        .WithPassword("secret")
        .WithDatabase("eventuous")
        .Build();

    public async Task InitializeAsync() {
        await _container.StartAsync();

        var connString = _container.GetConnectionString();

        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new NullLoggerFactory());
        services.AddEventuousPostgres(connString, SchemaName, true);
        services.AddAggregateStore<PostgresStore>();
        _provider = services.BuildServiceProvider();

        DataSource = _provider.GetRequiredService<NpgsqlDataSource>();
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = _provider.GetRequiredService<IEventStore>();
        AggregateStore = _provider.GetRequiredService<IAggregateStore>();
        ActivitySource.AddActivityListener(_listener);
        var initializer = _provider.GetRequiredService<IHostedService>();
        await initializer.StartAsync(default);
    }

    public async Task DisposeAsync() {
        await _container.DisposeAsync();
        _listener.Dispose();
    }
}
