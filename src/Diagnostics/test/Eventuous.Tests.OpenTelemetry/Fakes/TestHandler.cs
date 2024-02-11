namespace Eventuous.Tests.OpenTelemetry.Fakes;

class TestHandler(MessageCounter counter) : BaseEventHandler {
    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        await Task.Delay(10, context.CancellationToken);
        counter.Increment();

        return EventHandlingStatus.Success;
    }
}
