using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    Task HandleEvent(IMessageConsumeContext context, CancellationToken cancellationToken);
}