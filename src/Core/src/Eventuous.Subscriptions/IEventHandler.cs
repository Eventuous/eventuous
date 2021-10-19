using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    void SetLogger(SubscriptionLog subscriptionLogger);
    
    Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken);
}