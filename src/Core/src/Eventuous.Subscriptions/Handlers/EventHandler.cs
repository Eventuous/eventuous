// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using System.Text;

namespace Eventuous.Subscriptions;

using Context;
using Diagnostics;
using Logging;

/// <summary>
/// Base class for event handlers, which allows registering typed handlers for different event types
/// </summary>
[PublicAPI]
public abstract class EventHandler(TypeMapper? mapper = null) : BaseEventHandler {
    readonly Dictionary<Type, HandleUntypedEvent> _handlersMap = new();

    static readonly ValueTask<EventHandlingStatus> Ignored = new(EventHandlingStatus.Ignored);

    readonly TypeMapper _typeMapper = mapper ?? TypeMap.Instance;

    /// <summary>
    /// Register a handler for a particular event type
    /// </summary>
    /// <param name="handler">Function which handles an event</param>
    /// <typeparam name="T">Event type</typeparam>
    /// <exception cref="ArgumentException">Throws if a handler for the given event type has already been registered</exception>
    protected void On<T>(HandleTypedEvent<T> handler) where T : class {
        if (!_handlersMap.TryAdd(typeof(T), Handle)) {
            throw new ArgumentException($"Type {typeof(T).Name} already has a handler");
        }

        if (!_typeMapper.IsTypeRegistered<T>()) {
            SubscriptionsEventSource.Log.MessageTypeNotRegistered<T>();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask<EventHandlingStatus> Handle(IMessageConsumeContext context) {
            return context.Message is not T ? NoHandler() : HandleTypedEvent();

            async ValueTask<EventHandlingStatus> HandleTypedEvent() {
                var typedContext = context as MessageConsumeContext<T> ?? new MessageConsumeContext<T>(context);
                await handler(typedContext).NoContext();

                return EventHandlingStatus.Success;
            }

            ValueTask<EventHandlingStatus> NoHandler() {
                context.LogContext.MessageHandlerNotFound(DiagnosticName, context.MessageType);

                return Ignored;
            }
        }
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        if (!_handlersMap.TryGetValue(context.Message!.GetType(), out var handler)) {
            return EventHandlingStatus.Ignored;
        }

        return await handler(context).NoContext();
    }

    public override string ToString() {
        var sb = new StringBuilder();
        sb.AppendLine($"Handler: {GetType().Name}");

        foreach (var handler in _handlersMap) {
            sb.AppendLine($"Event: {handler.Key.Name}");
        }

        return sb.ToString();
    }

    delegate ValueTask<EventHandlingStatus> HandleUntypedEvent(IMessageConsumeContext evt);
}

public delegate ValueTask HandleTypedEvent<T>(MessageConsumeContext<T> consumeContext) where T : class;
