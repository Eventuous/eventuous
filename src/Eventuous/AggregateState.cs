using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Eventuous {
    public abstract record AggregateState<T> where T : AggregateState<T> {
        public virtual T When(object @event) {
            var eventType = @event.GetType();
            if (!_handlers.ContainsKey(eventType)) return (T) this;

            return _handlers[eventType](@event);
        }

        [PublicAPI]
        protected void On<TEvent>(Func<TEvent, T> handle) {
            if (!_handlers.TryAdd(typeof(TEvent), x => handle((TEvent) x))) {
                throw new InvalidOperationException($"Duplicate handler for {typeof(TEvent).Name}");
            }
        }

        readonly Dictionary<Type, Func<object, T>> _handlers = new();
    }
    
    public abstract record AggregateState<T, TId> : AggregateState<T>
        where T : AggregateState<T, TId>
        where TId : AggregateId {
        public TId Id { get; protected init; } = null!;

        internal T SetId(TId id) => (T) this with { Id = id };
    }
}
