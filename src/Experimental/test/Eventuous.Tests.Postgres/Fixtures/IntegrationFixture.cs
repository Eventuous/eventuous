using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using Eventuous.Diagnostics.Tracing;
using Eventuous.Postgresql;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using Npgsql;

namespace Eventuous.Tests.Postgres.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventStore     EventStore     { get; }
    public IAggregateStore AggregateStore { get; }
    public Fixture         Auto           { get; } = new();

    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    IntegrationFixture() {
        const string connString = "Host=localhost;Username=postgres;Password=secret;Database=eventuous;Include Error Detail=true;";
        NpgsqlConnection GetConnection() => new(connString);

        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new PostgresStore(GetConnection, new PostgresStoreOptions("__schema__"), Serializer);
        AggregateStore = new AggregateStore(EventStore);
        ActivitySource.AddActivityListener(_listener);
    }

    public async ValueTask DisposeAsync() => _listener.Dispose();
}
