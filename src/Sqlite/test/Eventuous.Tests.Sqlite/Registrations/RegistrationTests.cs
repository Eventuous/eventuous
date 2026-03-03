using Eventuous.Diagnostics.Tracing;
using Eventuous.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Eventuous.Tests.Sqlite.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Data Source=:memory:";

    [Test]
    public void Should_resolve_store_with_manual_registration() {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services => {
                        services.AddEventStore<SqliteStore>();
                        services.AddSingleton(new SqliteStoreOptions { ConnectionString = ConnectionString });
                    }
                )
            )
            .Build();
        var store = host.Services.GetRequiredService<IEventStore>();
        store.ShouldBeOfType<TracedEventStore>();
        var innerStore = ((TracedEventStore)store).Inner;
        innerStore.ShouldBeOfType<SqliteStore>();
    }

    [Test]
    public void Should_resolve_store_with_extensions() {
        var config = new Dictionary<string, string?> {
            ["sqlite:schema"]           = "test",
            ["sqlite:connectionString"] = ConnectionString
        };

        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(config))
                .ConfigureServices((ctx, services) => {
                        services.AddEventStore<SqliteStore>();
                        services.AddEventuousSqlite(ctx.Configuration.GetSection("sqlite"));
                    }
                )
            )
            .Build();
        var store = host.Services.GetService<IEventStore>();
        store.ShouldNotBeNull();
        var inner = ((store as TracedEventStore)!).Inner as SqliteStore;
        inner.ShouldNotBeNull();
        inner.Schema.SchemaName.ShouldBe("test");
    }
}
