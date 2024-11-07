// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use State<T> instead")]
public abstract record AggregateState<T> : State<T> where T : AggregateState<T>;

[PublicAPI]
public abstract record State<T> where T : State<T> {
    /// <summary>
    /// Function to apply event to the state object.
    /// </summary>
    /// <param name="event">Event to apply</param>
    /// <returns>New instance of state</returns>
    public virtual T When(object @event) {
        var eventType = @event.GetType();

        if (!_handlers.TryGetValue(eventType, out var handler)) return (T)this;

        return handler((T)this, @event);
    }

    /// <summary>
    /// Event handler that uses the event payload and creates a new instance of state using the data from the event.
    /// </summary>
    /// <param name="handle">Function to return new state instance after the event is applied</param>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <exception cref="Exceptions.DuplicateTypeException{T}">Thrown if another function already handles this event type</exception>
    [PublicAPI]
    protected void On<TEvent>(Func<T, TEvent, T> handle) {
        Ensure.NotNull(handle);

        if (!_handlers.TryAdd(typeof(TEvent), (state, evt) => handle(state, (TEvent)evt))) {
            throw new Exceptions.DuplicateTypeException<TEvent>();
        }
    }

    readonly Dictionary<Type, Func<T, object, T>> _handlers = new();
}

/// <summary>
/// State extended with identity. The identity is usually set automatically when the state is loaded from an event stream.
/// </summary>
/// <typeparam name="T">State type</typeparam>
/// <typeparam name="TId">Identity type</typeparam>
[PublicAPI]
public abstract record State<T, TId> : State<T> where T : State<T> where TId : Id {
    public TId Id { get; internal set; } = null!;
}
