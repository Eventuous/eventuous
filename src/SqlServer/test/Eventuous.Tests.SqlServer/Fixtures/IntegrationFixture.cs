using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.SqlServer;
using MicroElements.AutoFixture.NodaTime;
using Microsoft.Data.SqlClient;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;


namespace Eventuous.Tests.SqlServer.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore           EventStore     { get; }
    public IAggregateStore       AggregateStore { get; }
    public IFixture               Auto           { get; } = new Fixture().Customize(new NodaTimeCustomization());
    public GetSqlServerConnection GetConnection  { get; }
    public Faker                 Faker          { get; } = new();

    public string SchemaName => Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    IntegrationFixture() {
        const string connString = "Data Source=localhost;User Id=sa;Password=Secret_123;TrustServerCertificate=True;";
        
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
