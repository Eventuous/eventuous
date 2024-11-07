// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

[PublicAPI]
public abstract class Aggregate<T> where T : State<T>, new() {
    /// <summary>
    /// The collection of previously persisted events
    /// </summary>
    public object[] Original { get; protected set; } = [];

    /// <summary>
    /// Get the list of pending changes (new events) within the scope of the current operation.
    /// </summary>
    public IReadOnlyCollection<object> Changes => _changes.AsReadOnly();

    /// <summary>
    /// A collection with all the aggregate events, previously persisted and new
    /// </summary>
    public IEnumerable<object> Current => Original.Concat(_changes);

    /// <summary>
    /// Clears all the pending changes. Normally not used. It Can be used for testing purposes.
    /// </summary>
    public void ClearChanges() => _changes.Clear();

    /// <summary>
    /// The original version is the aggregate version we got from the store.
    /// It is used for optimistic concurrency to check if there were no changes made to the
    /// aggregate state between load and save for the current operation.
    /// </summary>
    public int OriginalVersion => Original.Length - 1;

    /// <summary>
    /// The current version is set to the original version when the aggregate is loaded from the store.
    /// It should increase for each state transition performed within the scope of the current operation.
    /// </summary>
    public int CurrentVersion => OriginalVersion + Changes.Count;

    readonly List<object> _changes = [];

    /// <summary>
    /// Adds an event to the list of pending changes.
    /// </summary>
    /// <param name="evt">New domain event</param>
    protected void AddChange(object evt) => _changes.Add(evt);

    /// <summary>
    /// Use this method to ensure you are operating on a new aggregate.
    /// </summary>
    /// <exception cref="DomainException"></exception>
    protected void EnsureDoesntExist(Func<Exception>? getException = null) {
        if (CurrentVersion >= 0)
            throw getException?.Invoke() ?? new DomainException($"{GetType().Name} already exists");
    }

    /// <summary>
    /// Use this method to ensure you are operating on an existing aggregate.
    /// </summary>
    /// <exception cref="DomainException"></exception>
    protected void EnsureExists(Func<Exception>? getException = null) {
        if (CurrentVersion < 0)
            throw getException?.Invoke() ?? new DomainException($"{GetType().Name} doesn't exist");
    }

    /// <summary>
    /// Applies a new event to the state, adds the event to the list of pending changes,
    /// and increases the current version.
    /// </summary>
    /// <param name="evt">New domain event to be applied</param>
    /// <returns>Previous and the new aggregate states</returns>
    protected (T PreviousState, T CurrentState) Apply<TEvent>(TEvent evt) where TEvent : class {
        AddChange(evt);
        var previous = State;
        State = State.When(evt);

        return (previous, State);
    }

    public void Load(IEnumerable<object?> events) {
        Original = events.Where(x => x != null).ToArray()!;
        State    = Original.Aggregate(State, Fold);

        return;

        static T Fold(T state, object evt) => state.When(evt);
    }

    /// <summary>
    /// Returns the current aggregate state. Cannot be mutated from the outside.
    /// </summary>
    public T State { get; private set; } = new();
}
