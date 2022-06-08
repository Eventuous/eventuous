using System.Runtime.CompilerServices;
using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;
using static Eventuous.Subscriptions.Diagnostics.SubscriptionsEventSource;

namespace Eventuous.Projections.MongoDB;

[PublicAPI]
public abstract class MongoProjection<T> : BaseEventHandler where T : ProjectedDocument {
    protected IMongoCollection<T> Collection { get; }

    readonly Dictionary<Type, ProjectUntypedEvent> _handlersMap = new();
    readonly TypeMapper                            _map;

    protected MongoProjection(IMongoDatabase database, TypeMapper? typeMap = null) {
        Collection = Ensure.NotNull(database).GetDocumentCollection<T>();
        _map       = typeMap ?? TypeMap.Instance;
    }

    readonly UpdateOptions _defaultUpdateOptions = new() { IsUpsert = true };

    /// <summary>
    /// Register a handler for a particular event type
    /// </summary>
    /// <param name="handler">Function which handles an event</param>
    /// <typeparam name="T">Event type</typeparam>
    /// <typeparam name="TEvent"></typeparam>
    /// <exception cref="ArgumentException">Throws if a handler for the given event type has already been registered</exception>
    protected void On<TEvent>(ProjectTypedEvent<T, TEvent> handler) where TEvent : class {
        if (!_handlersMap.TryAdd(typeof(TEvent), x => HandleInternal(x, handler))) {
            throw new ArgumentException($"Type {typeof(TEvent).Name} already has a handler");
        }

        if (!_map.IsTypeRegistered<TEvent>()) {
            Log.UnknownMessageType<TEvent>();
        }
    }

    protected void On<TEvent>(GetDocumentId<TEvent> getId, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(ctx => UpdateOperationTask(getId(ctx.Message), update => getUpdate(ctx.Message, update)));

    protected void On<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdate<TEvent, T> getUpdate) where TEvent : class
        => On<TEvent>(
            ctx => UpdateOperationTask(
                filter => getFilter(ctx.Message, filter),
                update => getUpdate(ctx.Message, update)
            )
        );

    protected void OnAsync<TEvent>(GetDocumentId<TEvent> getId, BuildUpdateAsync<TEvent, T> getUpdate)
        where TEvent : class {
        On<TEvent>(Project);

        async ValueTask<Operation<T>> Project(MessageConsumeContext<TEvent> ctx) {
            var id     = getId(ctx.Message);
            var update = await getUpdate(ctx.Message, Builders<T>.Update);
            return new UpdateOperation<T>(Builders<T>.Filter.Eq(x => x.Id, id), update);
        }
    }

    protected void OnAsync<TEvent>(BuildFilter<TEvent, T> getFilter, BuildUpdateAsync<TEvent, T> getUpdate)
        where TEvent : class {
        On<TEvent>(Project);

        async ValueTask<Operation<T>> Project(MessageConsumeContext<TEvent> ctx) {
            var filter = getFilter(ctx.Message, Builders<T>.Filter);
            var update = await getUpdate(ctx.Message, Builders<T>.Update);
            return new UpdateOperation<T>(filter, update);
        }
    }

    protected void On<TEvent>(
        Func<MongoOperationBuilder<TEvent, T>, MongoOperationBuilder<TEvent, T>.IMongoProjectorBuilder> configure
    )
        where TEvent : class {
        var builder   = new MongoOperationBuilder<TEvent, T>();
        var operation = configure(builder).Build();
        On(operation);
    }

    readonly ValueTask<EventHandlingStatus> _ignored = new(EventHandlingStatus.Ignored);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    ValueTask<Operation<T>> HandleInternal<TEvent>(IMessageConsumeContext context, ProjectTypedEvent<T, TEvent> handler)
        where TEvent : class {
        return context.Message is not TEvent ? NoHandler() : HandleTypedEvent();

        ValueTask<Operation<T>> HandleTypedEvent() {
            var typedContext = new MessageConsumeContext<TEvent>(context);
            return handler(typedContext);
        }

        ValueTask<Operation<T>> NoHandler() {
            Log.NoHandlerFound(DiagnosticName, context.MessageType);
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

        var status = EventHandlingStatus.Success;

        var task = update switch {
            OtherOperation<T> operation => operation.Execute(),
            CollectionOperation<T> col  => col.Execute(Collection, context.CancellationToken),
            UpdateOperation<T> upd      => ExecuteUpdate(upd),
            _                           => Ignore()
        };

        if (!task.IsCompleted) await task.NoContext();
        return status;

        Task Ignore() {
            status = EventHandlingStatus.Ignored;
            return Task.CompletedTask;
        }

        Task ExecuteUpdate(UpdateOperation<T> upd) {
            var streamPosition = context.StreamPosition;

            var (filterDefinition, updateDefinition) = upd;

            return Collection.UpdateOneAsync(
                filterDefinition,
                updateDefinition.Set(x => x.Position, streamPosition),
                _defaultUpdateOptions,
                context.CancellationToken
            );
        }
    }

    protected virtual ValueTask<Operation<T>> GetUpdate(object evt, ulong? position) => NoOp;

    ValueTask<Operation<T>> GetUpdate(IMessageConsumeContext context)
        => GetUpdate(context.Message!, context.StreamPosition);

    protected Operation<T> UpdateOperation(BuildFilter<T> filter, BuildUpdate<T> update)
        => new UpdateOperation<T>(filter(Builders<T>.Filter), update(Builders<T>.Update));

    protected ValueTask<Operation<T>> UpdateOperationTask(BuildFilter<T> filter, BuildUpdate<T> update)
        => new(UpdateOperation(filter, update));

    protected Operation<T> UpdateOperation(string id, BuildUpdate<T> update)
        => UpdateOperation(filter => filter.Eq(x => x.Id, id), update);

    protected ValueTask<Operation<T>> UpdateOperationTask(string id, BuildUpdate<T> update)
        => new(UpdateOperation(id, update));

    protected static readonly ValueTask<Operation<T>> NoOp = new((Operation<T>)null!);

    delegate ValueTask<Operation<T>> ProjectUntypedEvent(IMessageConsumeContext evt);
}

public delegate ValueTask<Operation<T>> ProjectTypedEvent<T, TEvent>(MessageConsumeContext<TEvent> consumeContext)
    where T : ProjectedDocument where TEvent : class;

// ReSharper disable once UnusedTypeParameter
public abstract record Operation<T>;

public record UpdateOperation<T>(FilterDefinition<T> Filter, UpdateDefinition<T> Update) : Operation<T>;

public record OtherOperation<T>(Func<Task> Execute) : Operation<T>;

public record CollectionOperation<T>(Func<IMongoCollection<T>, CancellationToken, Task> Execute) : Operation<T>;
