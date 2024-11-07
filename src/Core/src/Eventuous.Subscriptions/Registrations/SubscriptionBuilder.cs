// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable UnusedMethodReturnValue.Global

namespace Eventuous.Subscriptions.Registrations;

using Consumers;
using Context;
using Filters;

public abstract class SubscriptionBuilder(IServiceCollection services, string subscriptionId) {
    public string             SubscriptionId { get; } = subscriptionId;
    public IServiceCollection Services       { get; } = services;

    readonly List<ResolveHandler> _handlers = [];

    protected ConsumePipe     Pipe            { get; }      = new();
    protected ResolveConsumer ResolveConsumer { get; set; } = null!;

    protected IEventHandler[] ResolveHandlers(IServiceProvider sp) => _handlers.Select(x => x(sp)).ToArray();

    /// <summary>
    /// Adds an event handler to the subscription
    /// </summary>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public SubscriptionBuilder AddEventHandler<THandler>() where THandler : class, IEventHandler {
        Services.TryAddKeyedSingleton<THandler>(SubscriptionId);
        AddHandlerResolve(sp => sp.GetRequiredKeyedService<THandler>(SubscriptionId));

        return this;
    }

    /// <summary>
    /// Adds an event handler to the subscription
    /// </summary>
    /// <param name="getHandler">A function to resolve event handler using the service provider</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public SubscriptionBuilder AddEventHandler<THandler>(Func<IServiceProvider, THandler> getHandler) where THandler : class, IEventHandler {
        Services.TryAddKeyedSingleton<THandler>(SubscriptionId, (sp, _) => getHandler(sp));
        AddHandlerResolve(sp => sp.GetRequiredKeyedService<THandler>(SubscriptionId));

        return this;
    }

    /// <summary>
    /// Adds an event handler to the subscription by instance
    /// </summary>
    /// <param name="handler">Event handler instance</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public SubscriptionBuilder AddEventHandler<THandler>(THandler handler) where THandler : class, IEventHandler {
        AddHandlerResolve(_ => handler);

        return this;
    }

    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(Func<THandler, TWrappingHandler> getWrappingHandler)
        where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        Services.TryAddKeyedSingleton<THandler>(SubscriptionId);
        AddHandlerResolve(sp => getWrappingHandler(sp.GetRequiredKeyedService<THandler>(SubscriptionId)));

        return this;
    }

    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
            Func<IServiceProvider, THandler> getInnerHandler,
            Func<THandler, TWrappingHandler> getWrappingHandler
        ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        Services.TryAddKeyedSingleton(SubscriptionId, getInnerHandler);
        AddHandlerResolve(sp => getWrappingHandler(sp.GetRequiredKeyedService<THandler>(SubscriptionId)));

        return this;
    }

    /// <summary>
    /// Allows using a custom consumer instead of the <see cref="DefaultConsumer"/> one.
    /// Can also be used to change the default consumer instantiation.
    /// </summary>
    /// <param name="getConsumer">A function to resolve the consumer using the service provider</param>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder UseConsumer(Func<IServiceProvider, IEventHandler[], IMessageConsumer> getConsumer) {
        Ensure.NotNull(getConsumer);
        ResolveConsumer = sp => getConsumer(sp, ResolveHandlers(sp));

        return this;
    }

    /// <summary>
    /// Add a custom filter to the consume pipe, at the end of the pipe
    /// </summary>
    /// <param name="filter">The filter instance</param>
    /// <typeparam name="TIn">Inbound consume context type</typeparam>
    /// <typeparam name="TOut">Outbound consume context type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder AddConsumeFilterLast<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext where TOut : class, IBaseConsumeContext {
        Pipe.AddFilterLast(filter);

        return this;
    }

    /// <summary>
    /// Add a custom filter to the consume pipe, at the beginning of the pipe
    /// </summary>
    /// <param name="filter">The filter instance</param>
    /// <typeparam name="TIn">Inbound consume context type</typeparam>
    /// <typeparam name="TOut">Outbound consume context type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder AddConsumeFilterFirst<TIn, TOut>(IConsumeFilter<TIn, TOut> filter)
        where TIn : class, IBaseConsumeContext where TOut : class, IBaseConsumeContext {
        Pipe.AddFilterFirst(filter);

        return this;
    }

    void AddHandlerResolve(ResolveHandler resolveHandler)
        => _handlers.Add(
            sp => {
                var handler = resolveHandler(sp);

                return EventuousDiagnostics.Enabled ? new TracedEventHandler(handler) : handler;
            }
        );
}

public class SubscriptionBuilder<T, TOptions> : SubscriptionBuilder
    where T : EventSubscription<TOptions>
    where TOptions : SubscriptionOptions {
    public SubscriptionBuilder(IServiceCollection services, string subscriptionId) : base(services, subscriptionId) {
        ResolveConsumer  = ResolveDefaultConsumer;
        ConfigureOptions = options => options.SubscriptionId = subscriptionId;
    }

    T?                _resolvedSubscription;
    IMessageConsumer? _resolvedConsumer;

    public Action<TOptions> ConfigureOptions { get; private set; }

    /// <summary>
    /// Configure subscription options
    /// </summary>
    /// <param name="configureOptions">Subscription options configuration function</param>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder<T, TOptions> Configure(Action<TOptions>? configureOptions) {
        ConfigureOptions = Cfg;

        return this;

        void Cfg(TOptions options) {
            options.SubscriptionId = SubscriptionId;
            configureOptions?.Invoke(options);
        }
    }

    IMessageConsumer GetConsumer(IServiceProvider sp) {
        if (_resolvedConsumer != null) return _resolvedConsumer;

        _resolvedConsumer = ResolveConsumer(sp);

        return _resolvedConsumer;
    }

    IMessageConsumer ResolveDefaultConsumer(IServiceProvider sp) {
        _resolvedConsumer = new DefaultConsumer(ResolveHandlers(sp));

        return _resolvedConsumer;
    }

    public T ResolveSubscription(IServiceProvider sp) {
        if (_resolvedSubscription != null) {
            return _resolvedSubscription;
        }

        var consumer = GetConsumer(sp);

        if (EventuousDiagnostics.Enabled) {
            Pipe.AddFilterLast(new TracingFilter(consumer.GetType().Name));
        }

        Pipe.AddFilterLast(new ConsumerFilter(consumer));

        var opt      = Ensure.NotNull(sp.GetService<IOptionsMonitor<TOptions>>(), typeof(TOptions).Name);
        var provider = new KeyedServiceProvider(sp, SubscriptionId);

        var instance = ActivatorUtilities.CreateInstance<T>(provider, opt.Get(SubscriptionId), Pipe);
        _resolvedSubscription = instance;

        return instance;
    }
}

public delegate IEventHandler ResolveHandler(IServiceProvider sp);

public delegate IMessageConsumer ResolveConsumer(IServiceProvider sp);
