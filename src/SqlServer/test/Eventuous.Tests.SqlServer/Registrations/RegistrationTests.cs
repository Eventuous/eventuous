using Eventuous.Diagnostics.Tracing;
using Eventuous.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace Eventuous.Tests.SqlServer.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Server=localhost;User Id=sqlserver;Password=secret;Database=eventuous;TrustServerCertificate=True";

    [Test]
    public void Should_resolve_store_with_manual_registration() {
        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureServices(services => {
                        services.AddEventStore<SqlServerStore>();
                        services.AddSingleton(new SqlServerStoreOptions { ConnectionString = ConnectionString });
                    }
                )
            )
            .Build();
        var store = host.Services.GetRequiredService<IEventStore>();
        store.ShouldBeOfType<TracedEventStore>();
        var innerStore = ((TracedEventStore)store).Inner;
        innerStore.ShouldBeOfType<SqlServerStore>();
    }

    [Test]
    public void Should_resolve_store_with_extensions() {
        var config = new Dictionary<string, string?> {
            ["sqlserver:schema"]           = "test",
            ["sqlserver:connectionString"] = ConnectionString
        };

        using var host = new HostBuilder()
            .ConfigureWebHost(webHostBuilder => webHostBuilder
                .UseTestServer()
                .ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(config))
                .ConfigureServices((ctx, services) => {
                        services.AddEventStore<SqlServerStore>();
                        services.AddEventuousSqlServer(ctx.Configuration.GetSection("sqlserver"));
                    }
                )
            )
            .Build();
        var store = host.Services.GetService<IEventStore>();
        store.ShouldNotBeNull();
        var inner = ((store as TracedEventStore)!).Inner as SqlServerStore;
        inner.ShouldNotBeNull();
        inner.Schema.SchemaName.ShouldBe("test");
    }
}
