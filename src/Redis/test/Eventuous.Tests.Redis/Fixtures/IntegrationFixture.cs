using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using StackExchange.Redis;
using Eventuous.Redis;

namespace Eventuous.Tests.Redis.Fixtures;

public sealed class IntegrationFixture : IDisposable {
    public IEventStore      EventStore     { get; }
    public IAggregateStore  AggregateStore { get; }
    public GetRedisDatabase GetDatabase    { get; }

    readonly ActivityListener _listener = DummyActivityListener.Create();
    readonly GetRedisDatabase  _getDb;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        const string connString = "localhost";

        IDatabase GetDb() {
            var muxer = ConnectionMultiplexer.Connect(connString);
            return muxer.GetDatabase();
        }

        _getDb = GetDb;

        GetDatabase = GetDb;
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
        EventStore     = new RedisStore(GetDb, Serializer);
        AggregateStore = new AggregateStore(EventStore);
    }

    public Task Initialize()
        => RedisEventStoreModule.LoadModule(_getDb);

    public void Dispose()
        => _listener.Dispose();
}
