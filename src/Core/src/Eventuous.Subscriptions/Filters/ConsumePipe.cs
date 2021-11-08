using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public sealed class ConsumePipe : IAsyncDisposable {
    readonly List<object> _filters = new();
    
    public void AddFilter<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext 
        where TOut : class, IBaseConsumeContext {

        _send = ctx => InternalSend(ctx, _send);
        _filters.Add(filter);

        ValueTask InternalSend(IBaseConsumeContext context, Func<IBaseConsumeContext, ValueTask> send) {
            if (context is not TIn ctx)
                throw new InvalidOperationException(
                    $"Incoming context expected to be {typeof(TIn)} but it's {context.GetType().Name}"
                );
            
            return filter.Send(ctx, send);
        }
    }

    Func<IBaseConsumeContext, ValueTask> _send = null!;

    public ValueTask Send(IBaseConsumeContext context) => _send(context);

    public async ValueTask DisposeAsync() {
        foreach (var filter in _filters) {
            if (filter is IAsyncDisposable d) await d.DisposeAsync();
        }
    }
}