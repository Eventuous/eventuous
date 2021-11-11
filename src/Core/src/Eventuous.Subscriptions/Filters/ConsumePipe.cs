using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConsumePipe : IAsyncDisposable {
    readonly List<Filter> _filters = new();

    public void AddFilter<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext
        where TOut : class, IBaseConsumeContext {
        if (_filters.Count > 1 && _filters.Last().OutContext != typeof(TIn)) {
            throw new InvalidContextTypeException(_filters.Last().OutContext, typeof(TIn));
        }

        _send = ctx => InternalSend(ctx, _send);
        _filters.Add(new Filter(filter, typeof(TIn), typeof(TOut)));

        ValueTask InternalSend(
            IBaseConsumeContext                  context,
            Func<IBaseConsumeContext, ValueTask> send
        ) {
            if (context is not TIn ctx)
                throw new InvalidContextTypeException(typeof(TIn), context.GetType());

            return filter.Send(ctx, send);
        }
    }

    Func<IBaseConsumeContext, ValueTask> _send = null!;

    public ValueTask Send(IBaseConsumeContext context) => _send(context);

    public async ValueTask DisposeAsync() {
        foreach (var filter in _filters) {
            if (filter.FilterInstance is IAsyncDisposable d) await d.DisposeAsync();
        }
    }

    record Filter(object FilterInstance, Type InContext, Type OutContext);
}

public class InvalidContextTypeException : Exception {
    public InvalidContextTypeException(Type expected, Type actual)
        : base($"Expected context type is {expected.Name} for it is {actual.Name}") { }
}