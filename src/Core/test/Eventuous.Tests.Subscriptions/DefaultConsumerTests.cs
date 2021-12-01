using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.Subscriptions;

public class DefaultConsumerTests : IDisposable {
    static readonly Fixture           Fixture = new();
    readonly        TestEventListener _listener;

    public DefaultConsumerTests(ITestOutputHelper output) => _listener = new TestEventListener(output);

    [Fact]
    public async Task ShouldFailWhenHandlerNacks() {
        var handler  = new FailingHandler();
        var consumer = new DefaultConsumer(new IEventHandler[] { handler });
        var ctx      = Fixture.Create<MessageConsumeContext>();

        await consumer.Consume(ctx);

        ctx.HandlingResults.GetFailureStatus().Should().Be(EventHandlingStatus.Failure);
    }

    public void Dispose() => _listener.Dispose();
}

class FailingHandler : IEventHandler {
    public string DiagnosticName => "TestHandler";

    public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        throw new NotImplementedException();
    }
}
