// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

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
    public long OriginalVersion { get; protected set; } = -1;

    /// <summary>
    /// The current version is set to the original version when the aggregate is loaded from the store.
    /// It should increase for each state transition performed within the scope of the current operation.
    /// </summary>
    public long CurrentVersion => OriginalVersion + _changes.Count;

    readonly List<object> _changes = new();

    /// <summary>
    /// Restores the aggregate state from a collection of events, previously stored in the AggregateStore.
    /// </summary>
    /// <param name="events">Domain events from the aggregate stream</param>
    public abstract void Load(IEnumerable<object?> events);

    /// <summary>
    /// Restores the aggregate state from a snapshot, e.g. from cache.
    /// </summary>
    /// <param name="snapshot">The snapshot</param>
    public abstract void Load(Snapshot snapshot);

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
        if (CurrentVersion >= 0)
            throw getException?.Invoke()
               ?? new DomainException($"{GetType().Name} already exists");
    }

    /// <summary>
    /// Use this method to ensure you are operating on an existing aggregate.
    /// </summary>
    /// <exception cref="DomainException"></exception>
    protected void EnsureExists(Func<Exception>? getException = null) {
        if (CurrentVersion < 0)
            throw getException?.Invoke()
               ?? new DomainException($"{GetType().Name} doesn't exist");
    }

    /// <summary>
    /// Creates a snapshot of the current state and version
    /// </summary>
    /// <returns>The snapshot</returns>
    public virtual Snapshot CreateSnapshot() => throw new NotImplementedException();
}

public abstract class Aggregate<T> : Aggregate where T : State<T>, new() {

    protected Aggregate()
        => State = new T();

    /// <summary>
    /// Applies a new event to the state, adds the event to the list of pending changes,
    /// and increases the current version.
    /// </summary>
    /// <param name="evt">New domain event to be applied</param>
    /// <returns>The previous and the new aggregate states</returns>
    protected (T PreviousState, T CurrentState) Apply<TEvent>(TEvent evt) where TEvent : class {
        AddChange(evt);
        var previous = State;
        State = State.When(evt);
        return (previous, State);
    }

    public override void Load(IEnumerable<object?> events) {
        var originalEvents = events.Where(x => x != null)!.ToArray<object>();
        OriginalVersion += originalEvents.Length;
        // ReSharper disable once ConvertClosureToMethodGroup
        State = originalEvents.Aggregate(State, (state, evt) => Fold(state, evt));
    }

    public override void Load(Snapshot snapshot) {
        OriginalVersion = snapshot.Version;
        State = ((Snapshot<T>)snapshot).State;
    }

    public override Snapshot<T> CreateSnapshot() => new(State, CurrentVersion);

    static T Fold(T state, object evt)
        => state.When(evt);

    /// <summary>
    /// Returns the current aggregate state. Cannot be mutated from the outside.
    /// </summary>
    public T State { get; private set; }
}
