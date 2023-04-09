using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using StackExchange.Redis;
using Eventuous.Redis;

namespace Eventuous.Tests.Redis.Fixtures;

public sealed class IntegrationFixture : IAsyncDisposable {
    public IEventWriter     EventWriter    { get; }
    public IEventReader     EventReader    { get; }
    public IAggregateStore  AggregateStore { get; }
    public GetRedisDatabase GetDatabase    { get; }

    readonly ActivityListener _listener = DummyActivityListener.Create();

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public static IntegrationFixture Instance { get; } = new();

    public IntegrationFixture() {
        const string connString = "localhost";

        IDatabase GetDb() {
            var muxer = ConnectionMultiplexer.Connect(connString);
            return muxer.GetDatabase();
        }

        Module.LoadModule(GetDb).ConfigureAwait(false).GetAwaiter().GetResult();

        GetDatabase = GetDb;
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        var store = new RedisStore(GetDb, new RedisStoreOptions(), Serializer);
        EventWriter    = store;
        EventReader    = store;
        AggregateStore = new AggregateStore(store, store);
    }

    public ValueTask DisposeAsync() {
        _listener.Dispose();
        return default;
    }
}
