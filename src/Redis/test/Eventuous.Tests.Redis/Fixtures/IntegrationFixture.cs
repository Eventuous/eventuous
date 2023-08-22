using System.Diagnostics;
using System.Text.Json;
using Eventuous.Diagnostics;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using StackExchange.Redis;
using Eventuous.Redis;
using Testcontainers.Redis;

namespace Eventuous.Tests.Redis.Fixtures;

public sealed class IntegrationFixture : IAsyncLifetime {
    public IEventWriter     EventWriter { get; private set; } = null!;
    public IEventReader     EventReader { get; private set; } = null!;
    public GetRedisDatabase GetDatabase { get; private set; } = null!;

    readonly ActivityListener _listener       = DummyActivityListener.Create();
    RedisContainer            _redisContainer = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web)
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    );

    public IntegrationFixture() {
        DefaultEventSerializer.SetDefaultSerializer(Serializer);
    }

    public async Task InitializeAsync() {
        _redisContainer = new RedisBuilder().WithImage("redis:7.0.12-alpine").Build();

        await _redisContainer.StartAsync();
        var connString = _redisContainer.GetConnectionString();
        await Module.LoadModule(GetDb);

        GetDatabase = GetDb;
        var store = new RedisStore(GetDb, new RedisStoreOptions(), Serializer);
        EventWriter    = store;
        EventReader    = store;
        new AggregateStore(store, store);

        return;

        IDatabase GetDb() {
            var muxer = ConnectionMultiplexer.Connect(connString);

            return muxer.GetDatabase();
        }
    }

    public async Task DisposeAsync() {
        await _redisContainer.DisposeAsync();
        _listener.Dispose();
    }
}
