using System.Diagnostics.CodeAnalysis;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.TestHelpers.TUnit;

namespace Eventuous.Tests.Subscriptions;

[SuppressMessage("Performance", "CA1822:Mark members as static")]
public class DefaultConsumerTests : IDisposable {
    readonly TestEventListener _listener = new();

    [Test]
    public async Task ShouldFailWhenHandlerNacks() {
        var handler  = new FailingHandler();
        var consumer = new DefaultConsumer([handler]);
        var ctx      = TestContext.CreateContext();

        await consumer.Consume(ctx);

        await Assert.That(ctx.HandlingResults.GetFailureStatus()).IsEqualTo(EventHandlingStatus.Failure);
    }

    public void Dispose() => _listener.Dispose();
}

class FailingHandler : IEventHandler {
    public string DiagnosticName => "TestHandler";

    public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        throw new NotImplementedException();
    }
}
