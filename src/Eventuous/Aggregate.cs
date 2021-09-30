namespace Eventuous; 

[PublicAPI]
public abstract class Aggregate {
    /// <summary>
    /// Get the list of pending changes (new events) within the scope of the current operation.
    /// </summary>
    public IReadOnlyCollection<object> Changes => _changes.AsReadOnly();

    /// <summary>
    /// Clears all the pending changes. Normally not used. Can be used for testing purposes.
    /// </summary>
    public void ClearChanges()
        => _changes.Clear();

    /// <summary>
    /// The original version is the aggregate version we got from the store.
    /// It is used for optimistic concurrency, to check if there were no changes made to the
    /// aggregate state between load and save for the current operation.
    /// </summary>
    public int OriginalVersion { get; protected set; } = -1;

    /// <summary>
    /// The current version is set to the original version when the aggregate is loaded from the store.
    /// It should increase for each state transition performed within the scope of the current operation.
    /// </summary>
    public int CurrentVersion { get; protected set; } = -1;

    readonly List<object> _changes = new();

    /// <summary>
    /// Restores the aggregate state from a collection of events, previously stored in the <seealso cref="AggregateStore"/>
    /// </summary>
    /// <param name="events">Domain events from the aggregate stream</param>
    public abstract void Load(IEnumerable<object?> events);

    /// <summary>
    /// The fold operation for events loaded from the store, which restores the aggregate state.
    /// </summary>
    /// <param name="evt">Domain event to be applied to the state</param>
    public abstract void Fold(object evt);

    /// <summary>
    /// Get the aggregate id in a storage-friendly format. Allows using a value object as the aggregate id
    /// inside the model, which then gets converted to a string for storage purposes.
    /// </summary>
    /// <returns></returns>
    public abstract string GetId();

    /// <summary>
    /// Adds an event to the list of pending changes.
    /// </summary>
    /// <param name="evt">New domain event</param>
    protected void AddChange(object evt)
        => _changes.Add(evt);

    /// <summary>
    /// Use this method to ensure you are operating on a new aggregate.
    /// </summary>
    /// <exception cref="DomainException"></exception>
    protected void EnsureDoesntExist(Func<Exception>? getException = null) {
        if (CurrentVersion > -1)
            throw getException?.Invoke()
               ?? new DomainException($"{GetType().Name} already exists: {GetId()}");
    }

    /// <summary>
    /// Use this method to ensure you are operating on an existing aggregate.
    /// </summary>
    /// <exception cref="DomainException"></exception>
    protected void EnsureExists(Func<Exception>? getException = null) {
        if (CurrentVersion == -1)
            throw getException?.Invoke()
               ?? new DomainException($"{GetType().Name} doesn't exist: {GetId()}");
    }
}

[PublicAPI]
public abstract class Aggregate<T> : Aggregate where T : AggregateState<T>, new() {
    protected Aggregate()
        => State = new T();

    /// <summary>
    /// Applies a new event to the state, adds the event to the list of pending changes,
    /// and increases the current version.
    /// </summary>
    /// <param name="evt">New domain event to be applied</param>
    /// <returns>The previous and the new aggregate states</returns>
    protected virtual (T PreviousState, T CurrentState) Apply(object evt) {
        AddChange(evt);
        var previous = State;
        State = State.When(evt);
        CurrentVersion++;
        return (previous, State);
    }

    /// <inheritdoc />
    public override void Load(IEnumerable<object?> events)
        => State = events.Where(x => x != null).Aggregate(new T(), Fold!);

    /// <inheritdoc />
    public override void Fold(object evt)
        => State = Fold(State, evt);

    T Fold(T state, object evt) {
        OriginalVersion++;
        CurrentVersion++;
        return state.When(evt);
    }

    /// <summary>
    /// Returns the current aggregate state. Cannot be mutated from the outside.
    /// </summary>
    public T State { get; internal set; }
}

public abstract class Aggregate<T, TId> : Aggregate<T>
    where T : AggregateState<T, TId>, new()
    where TId : AggregateId {
    /// <inheritdoc />
    public override string GetId()
        => State.Id;
}