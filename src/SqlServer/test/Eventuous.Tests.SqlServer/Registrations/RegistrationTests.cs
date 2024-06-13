using Eventuous.SqlServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Tests.SqlServer.Registrations;

public class RegistrationTests {
    const string ConnectionString = "Server=localhost;User Id=sqlserver;Password=secret;Database=eventuous;TrustServerCertificate=True";

    [Fact]
    public void Should_resolve_store_with_manual_registration() {
        var builder = new WebHostBuilder();
        builder.Configure(_ => { });

        builder.ConfigureServices(
            services => {
                services.AddAggregateStore<SqlServerStore>();
                services.AddSingleton(new SqlServerStoreOptions() { ConnectionString = ConnectionString });
            }
        );
        var app            = builder.Build();
        var aggregateStore = app.Services.GetRequiredService<IAggregateStore>();
        aggregateStore.Should().NotBeNull();
    }

    [Fact]
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
                services.AddAggregateStore<SqlServerStore>();
                services.AddEventuousSqlServer(ctx.Configuration.GetSection("sqlserver"));
            }
        );
        var app            = builder.Build();
        var aggregateStore = app.Services.GetService<IAggregateStore>();
        aggregateStore.Should().NotBeNull();
        var reader = app.Services.GetService<IEventStore>();

        // 'TracedEventStore.Inner' is inaccessible due to its protection level...
        //var store = ((reader as TracedEventStore)!).Inner as SqlServerStore;
        //store.Should().NotBeNull();
        //store!.Schema.Should().Be("test");
    }
}
