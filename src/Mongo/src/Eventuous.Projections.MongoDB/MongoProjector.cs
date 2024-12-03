// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Logging;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Projections.MongoDB;

using Tools;

[Obsolete("Use MongoProjector instead")]
public abstract class MongoProjection<T>(IMongoDatabase database, ITypeMapper? typeMap = null) : MongoProjector<T>(database, null, typeMap)
    where T : ProjectedDocument;

public record MongoProjectionOptions<T> where T : Document {
    public string CollectionName { get; set; } = MongoCollectionName.For<T>();
}

/// <summary>
/// Base class for MongoDB projectors. Specify your event handlers in the constructor using <code>On</code> methods family.
/// </summary>
/// <typeparam name="T"></typeparam>
[UsedImplicitly]
public abstract class MongoProjector<T>(IMongoDatabase database, MongoProjectionOptions<T>? options = null, ITypeMapper? typeMap = null)
    : BaseEventHandler where T : ProjectedDocument {
    [PublicAPI]
    protected IMongoCollection<T> Collection { get; } =
        options != null ? Ensure.NotNull(database).GetCollection<T>(options?.CollectionName) : Ensure.NotNull<IMongoDatabase>(database).GetDocumentCollection<T>();

    readonly Dictionary<Type, ProjectUntypedEvent> _handlers = new();
    readonly ITypeMapper                           _map      = typeMap ?? TypeMap.Instance;

    /// <summary>
    /// Register a handler for a particular event type
    /// </summary>
    /// <param name="handler">Function which handles an event</param>
    /// <typeparam name="TEvent">Event type</typeparam>
    /// <exception cref="ArgumentException">Throws if a handler for the given event type has already been registered</exception>
    [PublicAPI]
    protected void On<TEvent>(ProjectTypedEvent<T, TEvent> handler) where TEvent : class {
        if (!_handlers.TryAdd(typeof(TEvent), x => HandleInternal(x, handler))) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.TryGetTypeName<TEvent>(out _)) {
            Log.MessageTypeNotRegistered<TEvent>();
        }
    }

    /// <summary>
    /// Define a projector operation using the <see cref="MongoOperationBuilder{TEvent,T}"/>
    /// </summary>
    /// <param name="configure">Builder configuration delegate</param>
    /// <typeparam name="TEvent">Event type</typeparam>
    [PublicAPI]
    protected void On<TEvent>(Func<MongoOperationBuilder<TEvent, T>, MongoOperationBuilder<TEvent, T>.IMongoProjectorBuilder> configure)
        where TEvent : class {
        var builder   = new MongoOperationBuilder<TEvent, T>();
        var operation = configure(builder).Build();
        On(operation);
    }

    [PublicAPI]
    protected void On<TEvent>(GetDocumentIdFromEvent<TEvent> getId, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Message)).UpdateFromContext(getUpdate));

    protected void On<TEvent>(GetDocumentIdFromStream getId, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Stream)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void On<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Filter(getFilter).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(GetDocumentIdFromEvent<TEvent> getId, BuildUpdateAsync<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Message)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(GetDocumentIdFromStream getId, BuildUpdateAsync<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Id(x => getId(x.Stream)).UpdateFromContext(getUpdate));

    [PublicAPI]
    protected void OnAsync<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdateAsync<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(b => b.UpdateOne.Filter(getFilter).UpdateFromContext(getUpdate));

    ValueTask<MongoProjectOperation<T>> HandleInternal<TEvent>(IMessageConsumeContext context, ProjectTypedEvent<T, TEvent> handler)
        where TEvent : class {
        return context.Message is not TEvent ? NoHandler() : HandleTypedEvent();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask<MongoProjectOperation<T>> HandleTypedEvent() {
            var typedContext = context as MessageConsumeContext<TEvent> ?? new MessageConsumeContext<TEvent>(context);

            return handler(typedContext);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        ValueTask<MongoProjectOperation<T>> NoHandler() {
            Logger.Current.MessageHandlerNotFound(DiagnosticName, context.MessageType);

            return NoOp;
        }
    }

    public override async ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context) {
        var updateTask = _handlers.TryGetValue(context.Message!.GetType(), out var handler)
            ? handler(context)
            : GetUpdate(context);

        var update = updateTask == NoOp
            ? null
            : updateTask.IsCompletedSuccessfully
                ? updateTask.Result
                : await updateTask.NoContext();

        if (update == null) {
            return EventHandlingStatus.Ignored;
        }

        var task = update.Execute(Collection, context.CancellationToken);

        if (!task.IsCompletedSuccessfully) await task.NoContext();

        return EventHandlingStatus.Success;
    }

    [PublicAPI]
    protected virtual ValueTask<MongoProjectOperation<T>> GetUpdate(object evt, ulong? position) => NoOp;

    ValueTask<MongoProjectOperation<T>> GetUpdate(IMessageConsumeContext context) => GetUpdate(context.Message!, context.StreamPosition);

    [PublicAPI]
    protected static readonly ValueTask<MongoProjectOperation<T>> NoOp = new((MongoProjectOperation<T>)null!);

    delegate ValueTask<MongoProjectOperation<T>> ProjectUntypedEvent(IMessageConsumeContext evt);
}

public delegate ValueTask<MongoProjectOperation<T>> ProjectTypedEvent<T, TEvent>(MessageConsumeContext<TEvent> consumeContext)
    where T : ProjectedDocument where TEvent : class;

public record MongoProjectOperation<T>(Func<IMongoCollection<T>, CancellationToken, Task> Execute);

public delegate ValueTask<WriteModel<T>> BuildWriteModel<T, TEvent>(MessageConsumeContext<TEvent> context) where T : ProjectedDocument where TEvent : class;
