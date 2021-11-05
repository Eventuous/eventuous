using System.Runtime.CompilerServices;
using System.Text;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Subscriptions;

/// <summary>
/// Base class for event handlers, which allows registering typed handlers for different event types
/// </summary>
[PublicAPI]
public abstract class EventHandler : IEventHandler {
    readonly Dictionary<Type, HandleUntypedEvent> _handlersMap = new();
    readonly Type                                 _myType;

    protected EventHandler(TypeMapper? mapper = null) {
        var map = mapper ?? TypeMap.Instance;
        map.EnsureTypesRegistered(_handlersMap.Keys);
        _myType = GetType();
    }

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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask Handle(IMessageConsumeContext context, CancellationToken cancellationToken) {
            return context.Message is not T ? NoHandler() : HandleTypedEvent();

            ValueTask HandleTypedEvent() {
                var typedContext = new MessageConsumeContext<T>(context);
                return handler(typedContext, cancellationToken);
            }

            ValueTask NoHandler() {
                context.Ignore(_myType);
                Log?.Warn("Handler can't process {Event}", typeof(T).Name);
                return default;
            }
        }
    }

    // TODO: This one is always null
    protected SubscriptionLog? Log { get; set; }

    public virtual async ValueTask HandleEvent(
        IMessageConsumeContext context,
        CancellationToken      cancellationToken
    ) {
        if (!_handlersMap.TryGetValue(context.Message!.GetType(), out var handler)) {
            context.Ignore(_myType);
            return;
        }

        try {
            await handler(context, cancellationToken);
            context.Ack(_myType);
        }
        catch (Exception e) {
            context.Nack(_myType, e);
        }
    }

    public override string ToString() {
        var sb = new StringBuilder();
        sb.AppendLine($"Handler: {GetType().Name}");

        foreach (var handler in _handlersMap) {
            sb.AppendLine($"Event: {handler.Key.Name}");
        }

        return sb.ToString();
    }

    delegate ValueTask HandleUntypedEvent(
        IMessageConsumeContext evt,
        CancellationToken      cancellationToken
    );
}

[PublicAPI]
[Obsolete("Use EventHandler instead")]
public abstract class TypedEventHandler : EventHandler { }

public delegate ValueTask HandleTypedEvent<T>(
    MessageConsumeContext<T> consumeContext,
    CancellationToken        cancellationToken
) where T : class;