using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.SqlServer;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Data.SqlClient;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventStore            EventStore    { get; private set; } = null!;
    public IFixture               Auto          { get; }              = new Fixture().Customize(new NodaTimeCustomization());
    public GetSqlServerConnection GetConnection { get; private set; } = null!;
    public Faker                  Faker         { get; }              = new();
    public Schema                 Schema        { get; set; }

    public string SchemaName { get; }

    public IntegrationFixture() => SchemaName = GetSchemaName();

    public string GetSchemaName() => Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ActivityListener _listener = DummyActivityListener.Create();

    SqlEdgeContainer _sqlServer = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public async Task InitializeAsync() {
        _sqlServer = new SqlEdgeBuilder()
            .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
            .WithAutoRemove(false)
            .WithCleanUp(false)
            .Build();
        await _sqlServer.StartAsync();

        Schema = new Schema(SchemaName);
        var connString = _sqlServer.GetConnectionString();
        GetConnection = () => GetConn(connString);
        await Schema.CreateSchema(GetConnection);
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore = new SqlServerStore(GetConnection, new SqlServerStoreOptions(SchemaName), Serializer);
        ActivitySource.AddActivityListener(_listener);

        return;

        SqlConnection GetConn(string connectionString) => new(connectionString);
    }

    public async Task DisposeAsync() {
        // await _sqlServer.DisposeAsync();
        _listener.Dispose();
    }
}
