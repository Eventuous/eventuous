using System.Diagnostics;
using System.Text.Json;
using Bogus;
using Eventuous.Diagnostics;
using Eventuous.Postgresql;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;

namespace Eventuous.Tests.Postgres.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore           EventStore     { get; }
    public IAggregateStore       AggregateStore { get; }
    public GetPostgresConnection GetConnection  { get; }

    readonly ActivityListener _listener = DummyActivityListener.Create();

    public string SchemaName { get; }

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        SchemaName = new Faker().Internet.UserName().Replace(".", "_").Replace("-", "").Replace(" ", "").ToLower();
        const string connString =
            "Host=localhost;Username=postgres;Password=secret;Database=eventuous;Include Error Detail=true;";

        NpgsqlConnection GetConn()
            => new(connString);

        var schema = new Schema(SchemaName);
        schema.CreateSchema(GetConn).ConfigureAwait(false).GetAwaiter().GetResult();

        GetConnection = GetConn;
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new PostgresStore(GetConn, new PostgresStoreOptions(SchemaName), Serializer);
        AggregateStore = new AggregateStore(EventStore);
        ActivitySource.AddActivityListener(_listener);
    }

    public ValueTask DisposeAsync() {
        _listener.Dispose();
        return default;
    }
}
