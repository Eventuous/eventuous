using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Eventuous.EventStoreDB.Subscriptions {
    [PublicAPI]
    public abstract class TypedEventHandler : IEventHandler {
        public abstract string SubscriptionId { get; }

        readonly Dictionary<Type, Func<object, long?, Task>> _handlersMap = new();

        protected void On<T>(Func<T, long?, Task> handler) where T : class {
            if (!_handlersMap.TryAdd(typeof(T), Handle)) {
                throw new ArgumentException($"Type {typeof(T).Name} already has a handler");
            }

            Task Handle(object evt, long? pos) => evt is not T typed ? Task.CompletedTask : handler(typed, pos);
        }

        public Task HandleEvent(object evt, long? position) =>
            !_handlersMap.TryGetValue(evt.GetType(), out var handler)
                ? Task.CompletedTask : handler(evt, position);
    }
}