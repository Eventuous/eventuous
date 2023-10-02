using Eventuous.SqlServer.Subscriptions;
using Eventuous.Subscriptions.Filters;
using Eventuous.Sut.Subs;
using Eventuous.Tests.SqlServer.Fixtures;

namespace Eventuous.Tests.SqlServer.Checkpointing;

public class CheckpointTest(IntegrationFixture fixture, ITestOutputHelper output) : IClassFixture<IntegrationFixture> {
    [Fact]
    public async Task Execute() {
        TypeMap.RegisterKnownEventTypes();

        var aggregateStore = new AggregateStore(fixture.EventStore);

        var checkpointStore = new SqlServerCheckpointStore(
            fixture.GetConnection,
            new SqlServerCheckpointStoreOptions {
                Schema = fixture.SchemaName
            }
        );
        var service = new TestCommandService(aggregateStore);
        var pipe    = new ConsumePipe();
        var handler = new TestHandler();
        pipe.AddDefaultConsumer(handler);

        var sub = new SqlServerAllStreamSubscription(
            fixture.GetConnection,
            new SqlServerAllStreamSubscriptionOptions {
                Schema         = fixture.SchemaName,
                SubscriptionId = "TestSubscription"
            },
            checkpointStore,
            pipe
        );
        var loggerFactory = new LoggerFactory().AddXunit(output, LogLevel.Debug);

        await sub.SubscribeWithLog(loggerFactory.CreateLogger(sub.SubscriptionId));
        var accounts = Enumerable.Range(0, 100000).Select(n => new TestAccount($"user{n:D4}")).ToList();
        await service.Handle(new InjectTestAccounts(accounts), default);
        
        while (handler.HandledCount < 100000) {
            await Task.Delay(100);
        }

        await sub.Unsubscribe(id => { }, default);
    }
}
