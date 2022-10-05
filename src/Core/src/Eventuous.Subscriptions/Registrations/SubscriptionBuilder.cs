// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Reflection;
using Eventuous.Diagnostics;
using Eventuous.Subscriptions.Consumers;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Eventuous.Subscriptions.Registrations;

public abstract class SubscriptionBuilder {
    public string             SubscriptionId { get; }
    public IServiceCollection Services       { get; }

    protected SubscriptionBuilder(IServiceCollection services, string subscriptionId) {
        SubscriptionId = subscriptionId;
        Services       = services;
    }

    readonly List<ResolveHandler> _handlers = new();

    protected ConsumePipe     Pipe            { get; }      = new();
    protected ResolveConsumer ResolveConsumer { get; set; } = null!;

    protected IEventHandler[] ResolveHandlers(IServiceProvider sp) => _handlers.Select(x => x(sp)).ToArray();

    /// <summary>
    /// Adds an event handler to the subscription
    /// </summary>
    /// <typeparam name="THandler">Event handler type</typeparam>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder AddEventHandler<THandler>()
        where THandler : class, IEventHandler {
        Services.TryAddSingleton<THandler>();
        AddHandlerResolve(sp => sp.GetRequiredService<THandler>());
        return this;
    }

    /// <summary>
    /// Adds an event handler to the subscription
    /// </summary>
    /// <param name="getHandler">A function to resolve event handler using the service provider</param>
    /// <typeparam name="THandler"></typeparam>
    /// <returns></returns>
    [PublicAPI]
    public SubscriptionBuilder AddEventHandler<THandler>(Func<IServiceProvider, THandler> getHandler)
        where THandler : class, IEventHandler {
        Services.TryAddSingleton(getHandler);
        AddHandlerResolve(sp => sp.GetRequiredService<THandler>());
        return this;
    }

    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
        Func<THandler, TWrappingHandler> getWrappingHandler
    ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        Services.TryAddSingleton<THandler>();
        AddHandlerResolve(sp => getWrappingHandler(sp.GetRequiredService<THandler>()));
        return this;
    }

    public SubscriptionBuilder AddCompositionEventHandler<THandler, TWrappingHandler>(
        Func<IServiceProvider, THandler> getInnerHandler,
        Func<THandler, TWrappingHandler> getWrappingHandler
    ) where THandler : class, IEventHandler where TWrappingHandler : class, IEventHandler {
        Services.TryAddSingleton(getInnerHandler);
        AddHandlerResolve(sp => getWrappingHandler(sp.GetRequiredService<THandler>()));
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
    public SubscriptionBuilder(IServiceCollection services, string subscriptionId)
        : base(services, subscriptionId) {
        ResolveConsumer  = ResolveDefaultConsumer;
        ConfigureOptions = options => options.SubscriptionId = subscriptionId;
    }

    T?                _resolvedSubscription;
    IMessageConsumer? _resolvedConsumer;

    public Action<TOptions>? ConfigureOptions { get; private set; }

    ParameterMap ParametersMap { get; } = new();

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

    public SubscriptionBuilder<T, TOptions> AddParameterMap<TService, TImplementation>()
        where TImplementation : class, TService {
        ParametersMap.Add<TService, TImplementation>();
        return this;
    }

    public SubscriptionBuilder<T, TOptions> AddParameterMap<TService, TImplementation>(Func<IServiceProvider, TImplementation> resolver)
        where TImplementation : class, TService {
        ParametersMap.Add<TService, TImplementation>(resolver);
        return this;
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
        const string subscriptionIdParameterName = "subscriptionId";

        if (_resolvedSubscription != null) return _resolvedSubscription;

        var consumer = GetConsumer(sp);

        if (EventuousDiagnostics.Enabled) {
            Pipe.AddFilterLast(new TracingFilter(consumer.GetType().Name));
        }

        Pipe.AddFilterLast(new ConsumerFilter(consumer));

        var constructors = typeof(T).GetConstructors<TOptions>();

        switch (constructors.Length) {
            case > 1:
                throw new ArgumentOutOfRangeException(
                    typeof(T).Name,
                    "Subscription type must have only one constructor with options argument"
                );
            case 0:
                constructors = typeof(T).GetConstructors<string>(subscriptionIdParameterName);
                break;
        }

        if (constructors.Length == 0) {
            throw new ArgumentOutOfRangeException(
                typeof(T).Name,
                "Subscription type must have at least one constructor with options or subscription id argument"
            );
        }

        var (ctor, parameter) = constructors[0];

        var args = ctor.GetParameters().Select(CreateArg).ToArray();

        if (ctor.Invoke(args) is not T instance)
            throw new InvalidOperationException($"Unable to instantiate {typeof(T)}");

        _resolvedSubscription = instance;
        return instance;

        object? CreateArg(ParameterInfo parameterInfo) {
            if (parameterInfo == parameter) {
                if (parameter.Name == subscriptionIdParameterName) {
                    return SubscriptionId;
                }

                var options = Ensure.NotNull(
                    sp.GetService<IOptionsMonitor<TOptions>>(),
                    typeof(TOptions).Name
                );

                return options.Get(SubscriptionId);
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            // ReSharper disable once InvertIf
            if (parameterInfo.ParameterType == typeof(ConsumePipe)) {
                return Pipe;
            }

            // ReSharper disable once ConvertIfStatementToReturnStatement
            if (ParametersMap.TryGetResolver(parameterInfo.ParameterType, out var resolver)) {
                return resolver!(sp);
            }

            return sp.GetService(parameterInfo.ParameterType);
        }
    }
}

static class TypeExtensionsForRegistrations {
    public static (ConstructorInfo Ctor, ParameterInfo? Options)[] GetConstructors<T>(
        this Type type,
        string?   name = null
    )
        => type
            .GetConstructors()
            .Select(
                x => (
                    Ctor: x,
                    Options: x.GetParameters()
                        .SingleOrDefault(
                            y => y.ParameterType == typeof(T) && (name == null || y.Name == name)
                        )
                )
            )
            .Where(x => x.Options != null)
            .ToArray()!;
}

public delegate IEventHandler ResolveHandler(IServiceProvider sp);

public delegate IMessageConsumer ResolveConsumer(IServiceProvider sp);
