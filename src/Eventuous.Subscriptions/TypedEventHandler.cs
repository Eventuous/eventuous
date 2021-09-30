namespace Eventuous.Subscriptions; 

/// <summary>
/// Base class for event handlers, which allows registering typed handlers for different event types
/// </summary>
[PublicAPI]
public abstract class EventHandler : IEventHandler {
    public abstract string SubscriptionId { get; }

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

        Task Handle(object evt, long? pos, CancellationToken cancellationToken)
            => evt is not T typed ? Task.CompletedTask : handler(typed, pos, cancellationToken);
    }

    public virtual Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) =>
        !_handlersMap.TryGetValue(evt.GetType(), out var handler)
            ? Task.CompletedTask : handler(evt, position, cancellationToken);

    delegate Task HandleUntypedEvent(object evt, long? position, CancellationToken cancellationToken);
}

[PublicAPI]
[Obsolete("Use EventHandler instead")]
public abstract class TypedEventHandler : EventHandler { }

public delegate Task HandleTypedEvent<in T>(T evt, long? position, CancellationToken cancellationToken);