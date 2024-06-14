using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers;

namespace Eventuous.Tests.Subscriptions;

public class DefaultConsumerTests(ITestOutputHelper output) : IDisposable {
    readonly TestEventListener _listener = new(output);

    static readonly Fixture Auto = new();

    [Fact]
    public async Task ShouldFailWhenHandlerNacks() {
        var handler  = new FailingHandler();
        var consumer = new DefaultConsumer([handler]);
        var ctx      = Auto.CreateContext(output);

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
