using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public abstract class BaseEventHandler : IEventHandler {
    protected BaseEventHandler() => DiagnosticName = GetType().Name;
    
    public string DiagnosticName { get; }

    public abstract ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}