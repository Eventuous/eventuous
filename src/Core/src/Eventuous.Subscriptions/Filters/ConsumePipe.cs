// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConsumePipe : IAsyncDisposable {
    readonly LinkedList<IConsumeFilter> _filters = new();

    public IEnumerable<object> RegisteredFilters => _filters.AsEnumerable();

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

    static ValueTask Move(LinkedListNode<IConsumeFilter>? node, IBaseConsumeContext context)
        => node == null ? default : node.Value.Send(context, node.Next);

    public async ValueTask DisposeAsync() {
        foreach (var filter in _filters) {
            if (filter is IAsyncDisposable d) {
                await d.DisposeAsync();
            }
        }
    }
}

public class InvalidContextTypeException : InvalidOperationException {
    public InvalidContextTypeException(Type expected, Type actual)
        : base($"Context type {expected.Name} is not assignable to {actual.Name}") { }
}

public class DuplicateFilterException : InvalidOperationException {
    public DuplicateFilterException(IConsumeFilter filter)
        : base($"Filter of type {filter.GetType()} is already registered") { }
}