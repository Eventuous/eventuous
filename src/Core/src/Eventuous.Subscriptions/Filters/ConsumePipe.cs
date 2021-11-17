using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConsumePipe : IAsyncDisposable {
    readonly LinkedList<Filter> _filters = new();

    public ConsumePipe AddFilterFirst<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext
        where TOut : class, IBaseConsumeContext {
        // Avoid adding one filter instance multiple times
        if (_filters.Any(x => x.FilterInstance == filter)) return this;

        if (_filters.Count > 0 && _filters.First().InContext != typeof(TOut)) {
            throw new InvalidContextTypeException(_filters.First().InContext, typeof(TOut));
        }

        _filters.AddFirst(
            new Filter(filter, typeof(TIn), typeof(TOut), (ctx, next) => InternalSend(filter, ctx, next))
        );
        return this;
    }

    public ConsumePipe AddFilter<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext
        where TOut : class, IBaseConsumeContext {
        // Avoid adding one filter instance multiple times
        if (_filters.Any(x => x.FilterInstance == filter)) return this;

        if (_filters.Count > 1 && _filters.Last().OutContext != typeof(TIn)) {
            throw new InvalidContextTypeException(_filters.Last().OutContext, typeof(TIn));
        }

        _filters.AddLast(
            new Filter(filter, typeof(TIn), typeof(TOut), (ctx, next) => InternalSend(filter, ctx, next))
        );

        return this;
    }

    static ValueTask InternalSend<TIn, TOut>(
        IConsumeFilter<TIn, TOut>             filter,
        IBaseConsumeContext                   context,
        Func<IBaseConsumeContext, ValueTask>? next
    ) where TIn : class, IBaseConsumeContext where TOut : class, IBaseConsumeContext {
        if (context is not TIn ctx)
            throw new InvalidContextTypeException(typeof(TIn), context.GetType());

        return filter.Send(ctx, next);
    }

    internal bool HasFilter<T>() => _filters.Any(x => x.FilterInstance.GetType() == typeof(T));

    public ValueTask Send(IBaseConsumeContext context) => Move(_filters.First, context);

    static ValueTask Move(LinkedListNode<Filter>? node, IBaseConsumeContext context)
        => node == null ? default : node.Value.SendForward(context, ctx => Move(node.Next, ctx));

    public async ValueTask DisposeAsync() {
        foreach (var filter in _filters) {
            if (filter.FilterInstance is IAsyncDisposable d) await d.DisposeAsync();
        }
    }

    delegate ValueTask SendForward(IBaseConsumeContext ctx, Func<IBaseConsumeContext, ValueTask>? next);

    record Filter(object FilterInstance, Type InContext, Type OutContext, SendForward SendForward);
}

public class InvalidContextTypeException : Exception {
    public InvalidContextTypeException(Type expected, Type actual)
        : base($"Expected context type is {expected.Name} for it is {actual.Name}") { }
}