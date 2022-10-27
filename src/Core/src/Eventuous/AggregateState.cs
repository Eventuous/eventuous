namespace Eventuous;

[Obsolete("Use State<T> instead")]
public abstract record AggregateState<T>: State<T> where T: AggregateState<T> { }

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
            throw new InvalidOperationException($"Duplicate handler for {typeof(TEvent).Name}");
        }
    }

    readonly Dictionary<Type, Func<T, object, T>> _handlers = new();
}
