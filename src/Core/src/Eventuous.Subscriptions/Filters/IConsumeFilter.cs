using Eventuous.Subscriptions.Context;

namespace Eventuous.Subscriptions.Filters;

public abstract class ConsumeFilter<TContext> : IConsumeFilter<TContext, TContext>
    where TContext : class, IBaseConsumeContext {
    public abstract ValueTask Send(TContext context, Func<TContext, ValueTask>? next);
}

public abstract class ConsumeFilter : ConsumeFilter<IMessageConsumeContext> { }

public interface IConsumeFilter<in TIn, out TOut>
    where TIn : class, IBaseConsumeContext
    where TOut : class, IBaseConsumeContext {
    ValueTask Send(TIn context, Func<TOut, ValueTask>? next);
}