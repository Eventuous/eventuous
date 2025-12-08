// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous;

/// <summary>
/// Represents a snapshot strategy that determines when and how to create snapshot events.
/// </summary>
/// <typeparam name="TState">The state type</typeparam>
sealed class SnapshotStrategy<TState>(
    Func<NewEvents, TState, bool> predicate,
    Func<NewEvents, TState, object> produce
) where TState : State<TState>, new() {
    public Func<NewEvents, TState, bool> Predicate { get; } = predicate;
    public Func<NewEvents, TState, object> Produce { get; } = produce;
}

