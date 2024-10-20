using Microsoft.Extensions.Logging;

namespace Eventuous.Tests.OpenTelemetry.Fakes;

class TestHandler(MessageCounter counter, ILogger<TestHandler> log) : BaseEventHandler {
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await Task.Delay(10, context.CancellationToken);
        counter.Increment();
        log.LogDebug("Processed {@Message}", context.Message);

        return EventHandlingStatus.Success;
    }
}
