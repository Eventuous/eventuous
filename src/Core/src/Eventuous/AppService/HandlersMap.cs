using Eventuous.Diagnostics;

namespace Eventuous;

record RegisteredHandler<T>(ExpectedState ExpectedState, Func<T, object, CancellationToken, ValueTask<T>> Handler);

class HandlersMap<T> : Dictionary<Type, RegisteredHandler<T>> {
    public void AddHandler<TCommand>(RegisteredHandler<T> handler) {
        if (ContainsKey(typeof(TCommand))) {
            EventuousEventSource.Log.CommandHandlerAlreadyRegistered<TCommand>();
            throw new Exceptions.CommandHandlerAlreadyRegistered<TCommand>();
        }

        Add(typeof(TCommand), handler);
    }
}
