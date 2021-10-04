namespace Eventuous.Subscriptions; 

public interface IEventHandler {
    Task HandleEvent(object evt, long? position, CancellationToken cancellationToken);
}