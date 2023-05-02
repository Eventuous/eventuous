// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[Obsolete("Use State<T> instead")]
public abstract record AggregateState<T> : State<T> where T : AggregateState<T>;

[PublicAPI]
public abstract record State<T> where T : State<T> {
    public virtual T When(object @event) {
        var eventType = @event.GetType();

        if (!_handlers.TryGetValue(eventType, out var handler)) return (T)this;

        return handler((T)this, @event);
    }

    [PublicAPI]
    protected void On<TEvent>(Func<T, TEvent, T> handle) {
        Ensure.NotNull(handle);

        if (!_handlers.TryAdd(typeof(TEvent), (state, evt) => handle(state, (TEvent)evt))) {
            throw new Exceptions.DuplicateTypeException<TEvent>();
        }
    }

    readonly Dictionary<Type, Func<T, object, T>> _handlers = new();
}

[PublicAPI]
public abstract record State<T, TId> : State<T> where T : State<T> where TId : Id {
    public TId Id { get; internal set; } = null!;
}
