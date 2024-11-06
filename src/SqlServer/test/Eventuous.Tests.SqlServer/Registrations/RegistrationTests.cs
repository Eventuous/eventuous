using Eventuous.Diagnostics.Tracing;
using Eventuous.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Tests.SqlServer.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Server=localhost;User Id=sqlserver;Password=secret;Database=eventuous;TrustServerCertificate=True";

    [Test]
    public void Should_resolve_store_with_manual_registration() {
        var builder = new WebHostBuilder();
        builder.Configure(_ => { });

        builder.ConfigureServices(
            services => {
                services.AddEventStore<SqlServerStore>();
                services.AddSingleton(new SqlServerStoreOptions { ConnectionString = ConnectionString });
            }
        );
        var app   = builder.Build();
        var store = app.Services.GetRequiredService<IEventStore>();
        store.Should().BeOfType<TracedEventStore>();
        var innerStore = ((TracedEventStore)store).Inner;
        innerStore.Should().BeOfType<SqlServerStore>();
    }

    [Test]
    public void Should_resolve_store_with_extensions() {
        var builder = new WebHostBuilder();

        var config = new Dictionary<string, string?> {
            ["sqlserver:schema"]           = "test",
            ["sqlserver:connectionString"] = ConnectionString
        };
        builder.ConfigureAppConfiguration(cfg => cfg.AddInMemoryCollection(config));
        builder.Configure(_ => { });

        builder.ConfigureServices(
            (ctx, services) => {
                services.AddEventStore<SqlServerStore>();
                services.AddEventuousSqlServer(ctx.Configuration.GetSection("sqlserver"));
            }
        );
        var app            = builder.Build();
        var store = app.Services.GetService<IEventStore>();
        store.Should().NotBeNull();
        var inner = ((store as TracedEventStore)!).Inner as SqlServerStore;
        inner.Should().NotBeNull();
        inner!.Schema.SchemaName.Should().Be("test");
    }
}
