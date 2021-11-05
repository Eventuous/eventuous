using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    ValueTask HandleEvent(IMessageConsumeContext context, CancellationToken cancellationToken);
}