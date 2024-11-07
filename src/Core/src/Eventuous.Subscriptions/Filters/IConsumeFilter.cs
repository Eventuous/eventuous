// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable UnusedTypeParameter

namespace Eventuous.Subscriptions.Filters;

using Context;

public interface IConsumeFilter {
    ValueTask Send(IBaseConsumeContext context, LinkedListNode<IConsumeFilter>? next);

    Type Consumes { get; }

    Type Produces { get; }
}

public interface IConsumeFilter<in TIn, TOut> : IConsumeFilter
    where TIn : class, IBaseConsumeContext
    where TOut : class, IBaseConsumeContext;

public abstract class ConsumeFilter<TIn, TOut> : IConsumeFilter<TIn, TOut>
    where TIn : class, IBaseConsumeContext where TOut : class, IBaseConsumeContext {
    protected abstract ValueTask Send(TIn context, LinkedListNode<IConsumeFilter>? next);

    public ValueTask Send(IBaseConsumeContext context, LinkedListNode<IConsumeFilter>? next) {
        if (context is not TIn ctx)
            throw new ArgumentException($"Context type expected to be {typeof(TIn)} but it is {context.GetType().Name}", nameof(context));

        if (next != null && !next.Value.Consumes.IsAssignableFrom(typeof(TOut)))
            throw new ArgumentException($"Next filter type expected to consume {typeof(TIn)} but it consumes {next.Value.Consumes}", nameof(next));

        return Send(ctx, next);
    }

    public Type Consumes => typeof(TIn);

    public Type Produces => typeof(TOut);
}

public abstract class ConsumeFilter<TContext> : ConsumeFilter<TContext, TContext> where TContext : class, IBaseConsumeContext;
