using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.SqlServer;
using Microsoft.Data.SqlClient;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;


namespace Eventuous.Tests.SqlServer.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore           EventStore     { get; }
    public IAggregateStore       AggregateStore { get; }
    public Fixture               Auto           { get; } = new();
    public GetSqlServerConnection GetConnection  { get; }
    public Faker                 Faker          { get; } = new();

    public string SchemaName => $"{Faker.Hacker.Adjective()}_{Faker.Hacker.Noun()}"
        .Replace("-", "").Replace(" ", "");

    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    IntegrationFixture() {
        const string connString = "Server=.;Database=eventuous;Integrated Security=true;TrustServerCertificate=True;";
        
        var schemaName = SchemaName;
        SqlConnection GetConn() => new(connString);

        var schema = new Schema(schemaName);
        schema.CreateSchema(GetConn).NoContext().GetAwaiter().GetResult();

        GetConnection = GetConn;
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new SqlServerStore(GetConn, new SqlServerStoreOptions(schemaName), Serializer);
        AggregateStore = new AggregateStore(EventStore);
        ActivitySource.AddActivityListener(_listener);
    }

    public ValueTask DisposeAsync() {
        _listener.Dispose();
        return default;
    }
}
