// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Subscriptions.Filters;

using Context;

public sealed class ConsumePipe : IAsyncDisposable {
    readonly LinkedList<IConsumeFilter> _filters = [];
    readonly List<object>               _owned   = [];

    /// <summary>
    /// Completed when the disposal that won <see cref="_disposing"/> is done, so callers that lost the race
    /// wait for the teardown instead of being told it finished.
    /// </summary>
    readonly TaskCompletionSource _disposed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    int _disposing;

    public IEnumerable<object> RegisteredFilters => _filters.AsEnumerable();

    /// <summary>
    /// Gives the pipe ownership of a component, so it gets disposed with the pipe. Used for event handlers
    /// created by the subscription builder, as nothing else would dispose them.
    /// </summary>
    /// <param name="component">Component to dispose with the pipe</param>
    internal void AddOwned(object component) => _owned.Add(component);

    public ConsumePipe AddFilterFirst<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext
        where TOut : class, IBaseConsumeContext {
        // Avoid adding one filter instance multiple times
        if (_filters.Any(x => x == filter)) return this;

        // Deny adding filter of the same type twice
        if (_filters.Any(x => x.GetType() == filter.GetType())) throw new DuplicateFilterException(filter);

        if (_filters.Count > 0 && !_filters.First().Consumes.IsAssignableFrom(typeof(TOut))) {
            throw new InvalidContextTypeException(_filters.First().Consumes, typeof(TOut));
        }

        _filters.AddFirst(filter);

        return this;
    }

    public ConsumePipe AddFilterLast<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext
        where TOut : class, IBaseConsumeContext {
        // Avoid adding one filter instance multiple times
        if (_filters.Any(x => x == filter)) return this;

        // Deny adding filter of the same type twice
        if (_filters.Any(x => x.GetType() == filter.GetType())) throw new DuplicateFilterException(filter);

        if (_filters.Count > 1 && !typeof(TIn).IsAssignableFrom(_filters.Last().Produces)) {
            throw new InvalidContextTypeException(_filters.Last().Produces, typeof(TIn));
        }

        _filters.AddLast(filter);

        return this;
    }

    public ValueTask Send(IBaseConsumeContext context) => Move(_filters.First, context);

    static ValueTask Move(LinkedListNode<IConsumeFilter>? node, IBaseConsumeContext context) => node == null ? default : node.Value.Send(context, node.Next);

    /// <summary>
    /// Disposes the filters and everything the pipe owns, exactly once. Concurrent callers, which the SignalR
    /// gateway produces when a disconnect races the gateway shutdown, all wait for the single teardown.
    /// </summary>
    public async ValueTask DisposeAsync() {
        // Exchange, not check-then-set: two callers reading a plain flag both pass and double-dispose.
        if (Interlocked.Exchange(ref _disposing, 1) != 0) {
            await _disposed.Task.NoContext();

            return;
        }

        try {
            foreach (var filter in _filters) {
                if (filter is IAsyncDisposable d) {
                    await d.DisposeAsync().NoContext();
                }
            }

            // After the filters, as they drain in-flight messages that still need their handlers, and in
            // reverse order of creation.
            for (var i = _owned.Count - 1; i >= 0; i--) {
                switch (_owned[i]) {
                    case IAsyncDisposable d:
                        await d.DisposeAsync().NoContext();

                        break;
                    case IDisposable d:
                        d.Dispose();

                        break;
                }
            }

            _disposed.TrySetResult();
        } catch (Exception e) {
            // The losing callers get the same failure rather than a teardown that looks clean.
            _disposed.TrySetException(e);

            throw;
        }
    }
}

public class InvalidContextTypeException(Type expected, Type actual) : InvalidOperationException($"Context type {expected.Name} is not assignable to {actual.Name}");

public class DuplicateFilterException(IConsumeFilter filter) : InvalidOperationException($"Filter of type {filter.GetType()} is already registered");
