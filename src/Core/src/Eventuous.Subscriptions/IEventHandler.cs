namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken);
}