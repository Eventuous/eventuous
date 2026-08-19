using System.Diagnostics;
using Eventuous.Diagnostics;
using StackExchange.Redis;
using Eventuous.Redis;
using Eventuous.TestHelpers;
using Testcontainers.Redis;
using TUnit.Core.Interfaces;

namespace Eventuous.Tests.Redis.Fixtures;

public sealed class IntegrationFixture : IAsyncInitializer, IAsyncDisposable {
    public IEventWriter     EventWriter { get; private set; } = null!;
    public IEventReader     EventReader { get; private set; } = null!;
    public GetRedisDatabase GetDatabase { get; private set; } = null!;

    readonly ActivityListener _listener       = DummyActivityListener.Create();
    RedisContainer            _redisContainer = null!;
    ConnectionMultiplexer     _muxer          = null!;

    IEventSerializer Serializer { get; } = new DefaultEventSerializer(TestPrimitives.DefaultOptions);

    public IntegrationFixture() => EventSerializer.SetDefault(Serializer);

    public async Task InitializeAsync() {
        _redisContainer = new RedisBuilder().WithImage("redis:7.0.12-alpine").Build();

        await _redisContainer.StartAsync();
        var connString = _redisContainer.GetConnectionString();

        // FLUSHDB in test teardown is an admin command; StackExchange.Redis 3.x enforces the
        // admin gate for raw commands issued through Execute as well.
        // abortConnect=false keeps the multiplexer retrying when the first connection attempt
        // races the freshly started container. The multiplexer is shared by all tests, as one
        // connection per process is how StackExchange.Redis is meant to be used.
        _muxer = await ConnectionMultiplexer.ConnectAsync($"{connString},allowAdmin=true,abortConnect=false");

        await Module.LoadModule(GetDb);

        GetDatabase = GetDb;
        var store = new RedisStore(GetDb, new(), Serializer);
        EventWriter = store;
        EventReader = store;

        return;

        IDatabase GetDb() => _muxer.GetDatabase();
    }

    public async ValueTask DisposeAsync() {
        _muxer.Dispose();
        await _redisContainer.DisposeAsync();
        _listener.Dispose();
    }
}
