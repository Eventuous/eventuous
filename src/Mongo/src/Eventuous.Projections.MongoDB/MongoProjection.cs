// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Projections.MongoDB;

[UsedImplicitly]
public abstract class MongoProjection<T> : BaseEventHandler where T : ProjectedDocument {
    [PublicAPI]
    protected IMongoCollection<T> Collection { get; }

    readonly Dictionary<Type, ProjectUntypedEvent> _handlersMap = new();
    readonly TypeMapper                            _map;

    protected MongoProjection(IMongoDatabase database, TypeMapper? typeMap = null) {
        Collection = Ensure.NotNull(database).GetDocumentCollection<T>();
        _map       = typeMap ?? TypeMap.Instance;
    }

    /// <summary>
    /// Register a handler for a particular event type
    /// </summary>
    /// <param name="handler">Function which handles an event</param>
    /// <typeparam name="T">Event type</typeparam>
    /// <typeparam name="TEvent"></typeparam>
    /// <exception cref="ArgumentException">Throws if a handler for the given event type has already been registered</exception>
    [PublicAPI]
    protected void On<TEvent>(ProjectTypedEvent<T, TEvent> handler) where TEvent : class {
        if (!_handlersMap.TryAdd(typeof(TEvent), x => HandleInternal(x, handler))) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.IsTypeRegistered<TEvent>()) {
            Logger.Current.MessageTypeNotFound<TEvent>();
        }
    }

    /// <summary>
    /// Define a projector operation using the <see cref="MongoOperationBuilder{TEvent,T}"/>
    /// </summary>
    /// <param name="configure">Builder configuration delegate</param>
    /// <typeparam name="TEvent">Event type</typeparam>
    [PublicAPI]
    protected void On<TEvent>(
        Func<MongoOperationBuilder<TEvent, T>, MongoOperationBuilder<TEvent, T>.IMongoProjectorBuilder> configure
    )
        where TEvent : class {
        var builder   = new MongoOperationBuilder<TEvent, T>();
        var operation = configure(builder).Build();
        On(operation);
    }

    [PublicAPI]
    protected void On<TEvent>(GetDocumentIdFromEvent<TEvent> getId, BuildUpdate<TEvent, T> getUpdate)
        where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Message)).UpdateFromContext(getUpdate));

    protected void On<TEvent>(GetDocumentIdFromStream getId, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Stream)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void On<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Filter(getFilter).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(GetDocumentIdFromEvent<TEvent> getId, BuildUpdateAsync<TEvent, T> getUpdate)
        where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Message!)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(GetDocumentIdFromStream getId, BuildUpdateAsync<TEvent, T> getUpdate)
        where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Stream)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdateAsync<TEvent, T> getUpdate)
        where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Filter(getFilter).UpdateFromContext(getUpdate));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTask<MongoProjectOperation<T>> HandleInternal<TEvent>(
        IMessageConsumeContext       context,
        ProjectTypedEvent<T, TEvent> handler
    )
        where TEvent : class {
        return context.Message is not TEvent ? NoHandler() : HandleTypedEvent();

        ValueTask<MongoProjectOperation<T>> HandleTypedEvent() {
            var typedContext = new MessageConsumeContext<TEvent>(context);
            return handler(typedContext);
        }

        ValueTask<MongoProjectOperation<T>> NoHandler() {
            Logger.Current.MessageHandlerNotFound(DiagnosticName, context.MessageType);
            return NoOp;
        }
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var updateTask = _handlersMap.TryGetValue(context.Message!.GetType(), out var handler)
            ? handler(context) : GetUpdate(context);

        var update = updateTask == NoOp
            ? null
            : updateTask.IsCompleted
                ? updateTask.Result
                : await updateTask.NoContext();

        if (update == null) {
            return EventHandlingStatus.Ignored;
        }

        var task = update.Execute(Collection, context.CancellationToken);

        if (!task.IsCompleted) await task.NoContext();
        return EventHandlingStatus.Success;
    }

    [PublicAPI]
    protected virtual ValueTask<MongoProjectOperation<T>> GetUpdate(object evt, ulong? position) => NoOp;

    ValueTask<MongoProjectOperation<T>> GetUpdate(IMessageConsumeContext context)
        => GetUpdate(context.Message!, context.StreamPosition);

    [PublicAPI] protected static readonly ValueTask<MongoProjectOperation<T>> NoOp = new(
        (MongoProjectOperation<T>)null!
    );

    delegate ValueTask<MongoProjectOperation<T>> ProjectUntypedEvent(IMessageConsumeContext evt);
}

public delegate ValueTask<MongoProjectOperation<T>> ProjectTypedEvent<T, TEvent>(
    MessageConsumeContext<TEvent> consumeContext
)
    where T : ProjectedDocument where TEvent : class;

public record MongoProjectOperation<T>(Func<IMongoCollection<T>, CancellationToken, Task> Execute);
