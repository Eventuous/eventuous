namespace Eventuous;

class IdMap<T> : Dictionary<Type, Func<object, CancellationToken, ValueTask<T>>> { }
