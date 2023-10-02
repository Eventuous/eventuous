using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Tests.SqlServer.Checkpointing;

public class TestHandler : IEventHandler {
    public string DiagnosticName => nameof(TestHandler);

    public ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        _count++;

        if (_count % 1000 == 0) {
            context.LogContext.DebugLog?.Log("Handled {Count} events", _count);
        }

        return ValueTask.FromResult(EventHandlingStatus.Success);
    }

    int _count;
    
    public int HandledCount => _count;
}
