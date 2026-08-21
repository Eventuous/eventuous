// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

// ReSharper disable UnusedMethodReturnValue.Global

namespace Eventuous.Subscriptions.Registrations;

using System.Diagnostics.CodeAnalysis;
using Consumers;
using Context;
using Filters;

public abstract class SubscriptionBuilder(IServiceCollection services, string subscriptionId) {
    public string             SubscriptionId { get; } = subscriptionId;
    public IServiceCollection Services       { get; } = services;

    readonly List<ResolveHandler> _handlers      = [];
    readonly HashSet<Type>        _handlerTypes  = [];
    readonly List<object>         _ownedHandlers = [];

    protected ConsumePipe     Pipe            { get; }      = new();
    protected ResolveConsumer ResolveConsumer { get; set; } = null!;

    protected IEventHandler[] ResolveHandlers(IServiceProvider sp) => [.. _handlers.Select(x => x(sp))];

    /// <summary>
    /// Adds an event handler to the subscription. The handler is registered in the container, keyed by
    /// <see cref="SubscriptionId"/>, so it can only be added once per subscription.
    /// </summary>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    /// <exception cref="ArgumentException">The same handler type is already registered for this subscription</exception>
    public SubscriptionBuilder AddEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler>() where THandler : class, IEventHandler {
        ReserveHandlerType<THandler>();
        Services.TryAddKeyedSingleton<THandler>(SubscriptionId);
        AddHandlerResolve(sp => sp.GetRequiredKeyedService<THandler>(SubscriptionId));

        return this;
    }

    /// <summary>
    /// Adds an event handler to the subscription. The handler is created once by the given function and kept by
    /// the subscription, it isn't registered in the container. Nothing disposes it, as the function might return
    /// a handler owned elsewhere; use the overload with <c>ownsHandler</c> for a handler the function creates.
    /// </summary>
    /// <param name="getHandler">A function to resolve event handler using the service provider</param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public SubscriptionBuilder AddEventHandler<THandler>(Func<IServiceProvider, THandler> getHandler) where THandler : class, IEventHandler
        => AddEventHandler(getHandler, false);

    /// <summary>
    /// Adds an event handler to the subscription. The handler is created once by the given function and kept by
    /// the subscription, it isn't registered in the container.
    /// </summary>
    /// <param name="getHandler">A function to resolve event handler using the service provider</param>
    /// <param name="ownsHandler">
    /// When <c>true</c>, the subscription owns the handler and disposes it when the subscription is disposed, if it
    /// implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>. Only set it when the function creates
    /// the handler: a handler the function resolves from the container is owned by the container, and disposing it
    /// would break the other components using it.
    /// </param>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    public SubscriptionBuilder AddEventHandler<THandler>(Func<IServiceProvider, THandler> getHandler, bool ownsHandler) where THandler : class, IEventHandler {
        THandler? handler = null;
        AddHandlerResolve(sp => handler ??= Own(getHandler(sp), ownsHandler));

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

    /// <summary>
    /// Adds a composition event handler to the subscription.
    /// The inner handler of type <typeparamref name="THandler"/> will be resolved from the container
    /// (keyed by <see cref="SubscriptionId"/>), and then wrapped by <typeparamref name="TWrappingHandler"/>
    /// using the provided factory.
    /// </summary>
    /// <typeparam name="THandler">Inner event handler type to be resolved from the service provider</typeparam>
    /// <typeparam name="TWrappingHandler">Wrapping event handler type produced by the factory</typeparam>
    /// <param name="getWrappingHandler">Factory that takes the resolved inner handler and returns the wrapping handler</param>
    /// <returns>The current <see cref="SubscriptionBuilder"/> instance</returns>
    /// <exception cref="ArgumentException">The same inner handler type is already registered for this subscription</exception>
    public SubscriptionBuilder AddCompositionEventHandler<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] THandler, TWrappingHandler>(
            Func<THandler, TWrappingHandler> getWrappingHandler
        )
        where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        ReserveHandlerType<THandler>();
        Services.TryAddKeyedSingleton<THandler>(SubscriptionId);
        AddHandlerResolve(sp => getWrappingHandler(sp.GetRequiredKeyedService<THandler>(SubscriptionId)));

        return this;
    }

    /// <summary>
    /// Adds a composition event handler to the subscription with a custom inner handler resolver.
    /// The inner handler is created via <paramref name="getInnerHandler"/> and then wrapped into
    /// <typeparamref name="TWrappingHandler"/> using <paramref name="getWrappingHandler"/>.
    /// The inner handler is created once and kept by the subscription, it isn't registered in the container.
    /// Nothing disposes it, as <paramref name="getInnerHandler"/> might return a handler owned elsewhere; use the
    /// overload with <c>ownsInnerHandler</c> for an inner handler the function creates. The wrapping handler
    /// decorates the inner one and is never disposed.
    /// </summary>
    /// <typeparam name="THandler">Inner event handler type</typeparam>
    /// <typeparam name="TWrappingHandler">Wrapping event handler type</typeparam>
    /// <param name="getInnerHandler">Function that resolves or creates the inner handler using the service provider</param>
    /// <param name="getWrappingHandler">Factory that produces the wrapping handler from the inner handler</param>
    /// <returns>The current <see cref="SubscriptionBuilder"/> instance</returns>
    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
            Func<IServiceProvider, THandler> getInnerHandler,
            Func<THandler, TWrappingHandler> getWrappingHandler
        ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler
        => AddCompositionEventHandler(getInnerHandler, getWrappingHandler, false);

    /// <summary>
    /// Adds a composition event handler to the subscription with a custom inner handler resolver.
    /// The inner handler is created via <paramref name="getInnerHandler"/> and then wrapped into
    /// <typeparamref name="TWrappingHandler"/> using <paramref name="getWrappingHandler"/>.
    /// The inner handler is created once and kept by the subscription, it isn't registered in the container.
    /// The wrapping handler decorates the inner one and is never disposed.
    /// </summary>
    /// <typeparam name="THandler">Inner event handler type</typeparam>
    /// <typeparam name="TWrappingHandler">Wrapping event handler type</typeparam>
    /// <param name="getInnerHandler">Function that resolves or creates the inner handler using the service provider</param>
    /// <param name="getWrappingHandler">Factory that produces the wrapping handler from the inner handler</param>
    /// <param name="ownsInnerHandler">
    /// When <c>true</c>, the subscription owns the inner handler and disposes it when the subscription is disposed,
    /// if it implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>. Only set it when
    /// <paramref name="getInnerHandler"/> creates the handler: a handler it resolves from the container is owned by
    /// the container, and disposing it would break the other components using it.
    /// </param>
    /// <returns>The current <see cref="SubscriptionBuilder"/> instance</returns>
    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
            Func<IServiceProvider, THandler> getInnerHandler,
            Func<THandler, TWrappingHandler> getWrappingHandler,
            bool                             ownsInnerHandler
        ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        THandler? innerHandler = null;
        AddHandlerResolve(sp => getWrappingHandler(innerHandler ??= Own(getInnerHandler(sp), ownsInnerHandler)));

        return this;
    }

    /// <summary>
    /// Adds a composition event handler to the subscription with a custom inner handler resolver.
    /// The inner handler is created via <paramref name="getInnerHandler"/> and then wrapped into
    /// <typeparamref name="TWrappingHandler"/> using <paramref name="getWrappingHandler"/>.
    /// The inner handler is created once and kept by the subscription, it isn't registered in the container.
    /// Nothing disposes it, as <paramref name="getInnerHandler"/> might return a handler owned elsewhere; use the
    /// overload with <c>ownsInnerHandler</c> for an inner handler the function creates. The wrapping handler
    /// decorates the inner one and is never disposed.
    /// </summary>
    /// <typeparam name="THandler">Inner event handler type</typeparam>
    /// <typeparam name="TWrappingHandler">Wrapping event handler type</typeparam>
    /// <param name="getInnerHandler">Function that resolves or creates the inner handler using the service provider</param>
    /// <param name="getWrappingHandler">Factory that produces the wrapping handler from the inner handler</param>
    /// <returns>The current <see cref="SubscriptionBuilder"/> instance</returns>
    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
            Func<IServiceProvider, THandler> getInnerHandler,
            Func<THandler, IServiceProvider, TWrappingHandler> getWrappingHandler
        ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler
        => AddCompositionEventHandler(getInnerHandler, getWrappingHandler, false);

    /// <summary>
    /// Adds a composition event handler to the subscription with a custom inner handler resolver.
    /// The inner handler is created via <paramref name="getInnerHandler"/> and then wrapped into
    /// <typeparamref name="TWrappingHandler"/> using <paramref name="getWrappingHandler"/>.
    /// The inner handler is created once and kept by the subscription, it isn't registered in the container.
    /// The wrapping handler decorates the inner one and is never disposed.
    /// </summary>
    /// <typeparam name="THandler">Inner event handler type</typeparam>
    /// <typeparam name="TWrappingHandler">Wrapping event handler type</typeparam>
    /// <param name="getInnerHandler">Function that resolves or creates the inner handler using the service provider</param>
    /// <param name="getWrappingHandler">Factory that produces the wrapping handler from the inner handler</param>
    /// <param name="ownsInnerHandler">
    /// When <c>true</c>, the subscription owns the inner handler and disposes it when the subscription is disposed,
    /// if it implements <see cref="IDisposable"/> or <see cref="IAsyncDisposable"/>. Only set it when
    /// <paramref name="getInnerHandler"/> creates the handler: a handler it resolves from the container is owned by
    /// the container, and disposing it would break the other components using it.
    /// </param>
    /// <returns>The current <see cref="SubscriptionBuilder"/> instance</returns>
    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
            Func<IServiceProvider, THandler>                   getInnerHandler,
            Func<THandler, IServiceProvider, TWrappingHandler> getWrappingHandler,
            bool                                               ownsInnerHandler
        ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        THandler? innerHandler = null;
        AddHandlerResolve(sp => getWrappingHandler(innerHandler ??= Own(getInnerHandler(sp), ownsInnerHandler), sp));

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

    /// <summary>
    /// Records a handler the subscription owns, so it gets disposed with the subscription. Ownership is stated by
    /// the caller rather than inferred: a handler factory is free to return a handler the container owns, and
    /// disposing that would break the other components using it.
    /// </summary>
    THandler Own<THandler>(THandler handler, bool owns) where THandler : class, IEventHandler {
        if (owns && handler is IDisposable or IAsyncDisposable) _ownedHandlers.Add(handler);

        return handler;
    }

    /// <summary>
    /// Hands the handlers created by the builder over to the pipe, which disposes them when the subscription
    /// is disposed.
    /// </summary>
    protected void TransferHandlersOwnership() {
        foreach (var handler in _ownedHandlers) {
            Pipe.AddOwned(handler);
        }

        _ownedHandlers.Clear();
    }

    /// <summary>
    /// Claims the container slot keyed by <see cref="SubscriptionId"/> for the given handler type. Two handlers of
    /// the same type would share that slot, so the subscription would silently dispatch the same instance twice.
    /// </summary>
    void ReserveHandlerType<THandler>() where THandler : class, IEventHandler {
        if (!_handlerTypes.Add(typeof(THandler))) {
            throw new ArgumentException(
                $"Event handler {typeof(THandler).Name} is already registered for subscription {SubscriptionId}. "
              + "Use the overload with a handler factory or instance to add several handlers of the same type."
            );
        }
    }

    void AddHandlerResolve(ResolveHandler resolveHandler)
        => _handlers.Add(sp => {
                var handler = resolveHandler(sp);

                return EventuousDiagnostics.Enabled ? new TracedEventHandler(handler) : handler;
            }
        );
}

public class SubscriptionBuilder
<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T,
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)]
    TOptions> : SubscriptionBuilder
    where T : EventSubscription<TOptions>
    where TOptions : SubscriptionOptions {
    /// <summary>
    /// Creates a new subscription builder for a specific subscription id.
    /// </summary>
    /// <param name="services">The service collection to register handlers and dependencies with</param>
    /// <param name="subscriptionId">The subscription identifier used to key registrations</param>
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

    /// <summary>
    /// Resolves and builds the subscription instance of type <typeparamref name="T"/>.
    /// Applies tracing and consumer filters to the consume pipe when diagnostics are enabled,
    /// resolves the configured consumer, and creates the subscription using options keyed by
    /// <code>SubscriptionId</code>.
    /// </summary>
    /// <param name="sp">Service provider used to resolve dependencies</param>
    /// <returns>The resolved and configured subscription instance</returns>
    public T ResolveSubscription(IServiceProvider sp) {
        if (_resolvedSubscription != null) {
            return _resolvedSubscription;
        }

        var consumer = GetConsumer(sp);
        TransferHandlersOwnership();

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
