using Eventuous.Diagnostics.Tracing;
using Eventuous.Postgresql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Assert = TUnit.Assertions.Assert;

namespace Eventuous.Tests.Postgres.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Host=localhost;Username=postgres;Password=secret;Database=eventuous;";

    [Test]
    public async Task Should_resolve_store_with_manual_registration() {
        var ds = new NpgsqlDataSourceBuilder(ConnectionString).Build();

        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services => {
                        services.AddEventStore<PostgresStore>();
                        services.AddSingleton(ds);
                        services.AddSingleton(new PostgresStoreOptions());
                    }
                )
            )
            .Build();

        var aggregateStore = host.Services.GetRequiredService<IEventStore>();
        await Assert.That(aggregateStore).IsNotNull();
    }

    [Test]
    public async Task Should_resolve_store_with_extensions() {
        var config = new Dictionary<string, string?> {
            ["postgres:schema"]           = "test",
            ["postgres:connectionString"] = ConnectionString
        };

        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(config))
                .ConfigureServices((ctx, services) => {
                        services.AddEventStore<PostgresStore>();
                        services.AddEventuousPostgres(ctx.Configuration.GetSection("postgres"));
                    }
                )
            )
            .Build();

        var reader       = host.Services.GetService<IEventStore>();
        var npgSqlReader = ((reader as TracedEventStore)!).Inner as PostgresStore;
        await Assert.That(npgSqlReader).IsNotNull();
        await Assert.That(npgSqlReader!.Schema.StreamMessage).IsEqualTo("test.stream_message");
    }
}
