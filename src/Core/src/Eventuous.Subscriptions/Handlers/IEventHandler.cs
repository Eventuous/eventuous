using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    string DiagnosticName { get; }
    
    ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}