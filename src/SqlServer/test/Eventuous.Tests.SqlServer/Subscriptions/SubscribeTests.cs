using Eventuous.SqlServer.Subscriptions;
using Eventuous.Tests.Subscriptions.Base;
using Testcontainers.SqlEdge;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Subscriptions;

public class SubscribeToAll(ITestOutputHelper outputHelper)
    : SubscribeToAllBase<SqlEdgeContainer, SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, SqlServerCheckpointStore>(
        outputHelper,
        new SubscriptionFixture<SqlServerAllStreamSubscription, SqlServerAllStreamSubscriptionOptions, TestEventHandler>(_ => { }, outputHelper, false)
    ) {
}

public class SubscribeToStream(ITestOutputHelper outputHelper, StreamNameFixture streamNameFixture)
    : SubscribeToStreamBase<SqlEdgeContainer, SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, SqlServerCheckpointStore>(
            outputHelper,
            streamNameFixture.StreamName,
            new SubscriptionFixture<SqlServerStreamSubscription, SqlServerStreamSubscriptionOptions, TestEventHandler>(
                opt => ConfigureOptions(opt, streamNameFixture),
                outputHelper,
                false
            )
        ),
        IClassFixture<StreamNameFixture> {
    static void ConfigureOptions(SqlServerStreamSubscriptionOptions options, StreamNameFixture streamNameFixture) {
        options.Stream = streamNameFixture.StreamName;
    }
}

public class StreamNameFixture {
    static readonly Fixture Auto = new();

    public StreamName StreamName = new(Auto.Create<string>());
}