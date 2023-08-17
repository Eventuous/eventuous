using System.Diagnostics;
using System.Text.Json;
using Bogus;
using DotNet.Testcontainers.Builders;
using Eventuous.Diagnostics;
using Eventuous.SqlServer;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Data.SqlClient;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventStore            EventStore     { get; private set; } = null!;
    public IAggregateStore        AggregateStore { get; set; }         = null!;
    public IFixture               Auto           { get; }              = new Fixture().Customize(new NodaTimeCustomization());
    public GetSqlServerConnection GetConnection  { get; private set; } = null!;
    public Faker                  Faker          { get; }              = new();

    public string SchemaName { get; }

    public IntegrationFixture() => SchemaName = GetSchemaName();

    public string GetSchemaName() => Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ActivityListener _listener = DummyActivityListener.Create();

    MsSqlContainer _sqlServer = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public async Task InitializeAsync() {
        _sqlServer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("EdgeTelemetry starting up"))
            .Build();
        await _sqlServer.StartAsync();

        var schema     = new Schema(SchemaName);
        var connString = _sqlServer.GetConnectionString();
        GetConnection = () => GetConn(connString);
        await schema.CreateSchema(GetConnection);
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new SqlServerStore(GetConnection, new SqlServerStoreOptions(SchemaName), Serializer);
        AggregateStore = new AggregateStore(EventStore);
        ActivitySource.AddActivityListener(_listener);

        return;

        SqlConnection GetConn(string connectionString) => new(connectionString);
    }

    public async Task DisposeAsync() {
        await _sqlServer.DisposeAsync();
        _listener.Dispose();
    }
}
