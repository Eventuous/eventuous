using Eventuous.Diagnostics.Logging;
using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Eventuous.Tests.SqlServer.Fixtures;

namespace Eventuous.Tests.SqlServer.Checkpointing;

public class CheckpointTest : IClassFixture<IntegrationFixture>, IDisposable {
    readonly IDisposable        _listener;
    readonly IntegrationFixture _fixture;
    readonly ILoggerFactory     _loggerFactory;

    public CheckpointTest(IntegrationFixture fixture, ITestOutputHelper output) {
        _fixture       = fixture;
        _loggerFactory = new LoggerFactory().AddXunit(output, LogLevel.Debug);
        _listener      = new LoggingEventListener(_loggerFactory);
    }

    [Fact]
    public async Task EmitMassiveNumberOfEventsAndEnsureCheckpointingWorks() {
        TypeMap.RegisterKnownEventTypes();
        
        // Change this number to produce more events. It's set to 1000 for the sake of CI.
        const int count = 1000;

        var aggregateStore = new AggregateStore(_fixture.EventStore);

        var checkpointStore = new SqlServerCheckpointStore(
            new SqlServerCheckpointStoreOptions {
                ConnectionString = _fixture.GetConnectionString(),
                Schema = _fixture.SchemaName
            }
        );
        var service = new TestCommandService(aggregateStore);
        var pipe    = new ConsumePipe();
        var handler = new TestHandler();
        pipe.AddDefaultConsumer(handler);

        var sub = new SqlServerAllStreamSubscription(
            new SqlServerAllStreamSubscriptionOptions {
                ConnectionString          = _fixture.GetConnectionString(),
                Schema                    = _fixture.SchemaName,
                SubscriptionId            = "TestSubscription",
                CheckpointCommitBatchSize = count / 100,
            },
            checkpointStore,
            pipe,
            _loggerFactory
        );
        await sub.SubscribeWithLog(_loggerFactory.CreateLogger(sub.SubscriptionId));
        var accounts = Enumerable.Range(0, count).Select(n => new TestAccount($"user{n:D4}")).ToList();
        await service.Handle(new InjectTestAccounts(accounts), default);

        while (handler.HandledCount < count) {
            await Task.Delay(100);
        }

        await sub.Unsubscribe(id => { }, default);

        var checkpoint = await checkpointStore.GetLastCheckpoint(sub.SubscriptionId, default);
        checkpoint.Position.Should().Be(count - 1);
    }

    public void Dispose() => _listener.Dispose();
}
