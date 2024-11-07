// Copyright (C) Eventuous HQ OÜ. All rights reserved
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
public abstract class EventHandler(ITypeMapper? mapper = null) : BaseEventHandler {
    readonly Dictionary<Type, HandleUntypedEvent> _handlersMap = new();

    static readonly ValueTask<EventHandlingStatus> Ignored = new(EventHandlingStatus.Ignored);

    readonly ITypeMapper _typeMapper = mapper ?? TypeMap.Instance;

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

        if (!_typeMapper.TryGetTypeName<T>(out _)) {
            SubscriptionsEventSource.Log.MessageTypeNotRegistered<T>();
        }

        return;

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

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context)
        => !_handlersMap.TryGetValue(context.Message!.GetType(), out var handler) ? EventHandlingStatus.Ignored : await handler(context).NoContext();

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
