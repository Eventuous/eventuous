namespace Eventuous.Subscriptions; 

/// <summary>
/// Base class for event handlers, which allows registering typed handlers for different event types
/// </summary>
[PublicAPI]
public abstract class EventHandler : IEventHandler {
    readonly Dictionary<Type, HandleUntypedEvent> _handlersMap = new();

    /// <summary>
    /// Register a handler for a particular event type
    /// </summary>
    /// <param name="handler">Function which handles an event</param>
    /// <typeparam name="T">Event type</typeparam>
    /// <exception cref="ArgumentException">Throws if a handler for the given event type has already been registered</exception>
    protected void On<T>(HandleTypedEvent<T> handler) where T : class {
        if (!_handlersMap.TryAdd(typeof(T), Handle)) {
            throw new ArgumentException($"Type {typeof(T).Name} already has a handler");
        }

        Task Handle(ReceivedEvent evt,  CancellationToken cancellationToken) {
            return evt.Payload is not T ? Task.CompletedTask : HandleTypedEvent();

            Task HandleTypedEvent() {
                var typedEvent = new ReceivedEvent<T>(evt);
                return handler(typedEvent, cancellationToken);
            }
        }
    }

    public virtual Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken) =>
        !_handlersMap.TryGetValue(evt.Payload!.GetType(), out var handler)
            ? Task.CompletedTask : handler(evt, cancellationToken);

    delegate Task HandleUntypedEvent(ReceivedEvent evt, CancellationToken cancellationToken);
}

[PublicAPI]
[Obsolete("Use EventHandler instead")]
public abstract class TypedEventHandler : EventHandler { }

public delegate Task HandleTypedEvent<T>(ReceivedEvent<T> evt, CancellationToken cancellationToken);