using Eventuous.Diagnostics.Tracing;
using Eventuous.Postgresql;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Eventuous.Tests.Postgres.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Host=localhost;Username=postgres;Password=secret;Database=eventuous;";

    [Fact]
    public void Should_resolve_store_with_manual_registration() {
        var ds      = new NpgsqlDataSourceBuilder(ConnectionString).Build();
        var builder = new WebHostBuilder();
        builder.Configure(_ => { });

        builder.ConfigureServices(
            services => {
                services.AddEventStore<PostgresStore>();
                services.AddSingleton(ds);
                services.AddSingleton(new PostgresStoreOptions());
            }
        );
        var app            = builder.Build();
        var aggregateStore = app.Services.GetRequiredService<IEventStore>();
        aggregateStore.Should().NotBeNull();
    }

    [Fact]
    public void Should_resolve_store_with_extensions() {
        var builder = new WebHostBuilder();
        var config  = new Dictionary<string, string?> {
            ["postgres:schema"] = "test",
            ["postgres:connectionString"] = ConnectionString
        };
        builder.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(config));
        builder.Configure(_ => { });

        builder.ConfigureServices(
            (ctx, services) => {
                services.AddEventStore<PostgresStore>();
                services.AddEventuousPostgres(ctx.Configuration.GetSection("postgres"));
            }
        );
        var app            = builder.Build();
        var reader       = app.Services.GetService<IEventStore>();
        var npgSqlReader = ((reader as TracedEventStore)!).Inner as PostgresStore;
        npgSqlReader.Should().NotBeNull();
        npgSqlReader!.Schema.StreamMessage.Should().Be("test.stream_message");
    }
}
