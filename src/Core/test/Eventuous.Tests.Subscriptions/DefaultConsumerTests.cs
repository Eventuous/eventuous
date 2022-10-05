using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.Subscriptions;

public class DefaultConsumerTests : IDisposable {
    readonly ITestOutputHelper _output;
    readonly TestEventListener _listener;

    static readonly Fixture Auto = new();

    public DefaultConsumerTests(ITestOutputHelper output) {
        _output   = output;
        _listener = new TestEventListener(output);
    }

    [Fact]
    public async Task ShouldFailWhenHandlerNacks() {
        var handler  = new FailingHandler();
        var consumer = new DefaultConsumer(new IEventHandler[] { handler });
        var ctx      = Auto.CreateContext(_output);

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
