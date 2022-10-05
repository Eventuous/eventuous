using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.Postgresql;
using MicroElements.AutoFixture.NodaTime;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;

namespace Eventuous.Tests.Postgres.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore           EventStore     { get; }
    public IAggregateStore       AggregateStore { get; }
    public IFixture              Auto           { get; } = new Fixture().Customize(new NodaTimeCustomization());
    public GetPostgresConnection GetConnection  { get; }
    public Faker                 Faker          { get; } = new();

    public string SchemaName => Faker.Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();

    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    IntegrationFixture() {
        const string connString =
            "Host=localhost;Username=postgres;Password=secret;Database=eventuous;Include Error Detail=true;";

        var schemaName = SchemaName;

        NpgsqlConnection GetConn() => new(connString);

        var schema = new Schema(schemaName);
        schema.CreateSchema(GetConn).NoContext().GetAwaiter().GetResult();

        GetConnection = GetConn;
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new PostgresStore(GetConn, new PostgresStoreOptions(schemaName), Serializer);
        AggregateStore = new AggregateStore(EventStore);
        ActivitySource.AddActivityListener(_listener);
    }

    public ValueTask DisposeAsync() {
        _listener.Dispose();
        return default;
    }
}
