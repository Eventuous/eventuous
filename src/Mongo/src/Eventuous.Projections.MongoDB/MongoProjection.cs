using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;
using Microsoft.Extensions.Logging;

namespace Eventuous.Projections.MongoDB;

[PublicAPI]
public abstract class MongoProjection<T> : IEventHandler
    where T : ProjectedDocument {
    protected IMongoCollection<T> Collection { get; }
    protected ILogger?            Log        { get; set; }

    protected MongoProjection(IMongoDatabase database, ILoggerFactory? loggerFactory) {
        Log        = loggerFactory?.CreateLogger(GetType());
        Collection = Ensure.NotNull(database, nameof(database)).GetDocumentCollection<T>();
        _myType    = GetType();
    }

    Type _myType;

    public async ValueTask HandleEvent(IMessageConsumeContext context) {
        var updateTask = GetUpdate(context);
        var update     = updateTask == NoOp ? null : await updateTask.NoContext();

        if (update == null) {
            Log?.LogDebug("No handler for {Event}", context.MessageType);
            context.Ignore(_myType);
            return;
        }

        Log?.LogDebug("Projecting {Event}", context.MessageType);

        var task = update switch {
            OtherOperation<T> operation => operation.Execute(),
            CollectionOperation<T> col  => col.Execute(Collection, context.CancellationToken),
            UpdateOperation<T> upd      => ExecuteUpdate(upd),
            _                           => Task.CompletedTask
        };

        await task.NoContext();

        Task ExecuteUpdate(UpdateOperation<T> upd) {
            var streamPosition = context is MessageConsumeContext ctx ? (long)ctx.StreamPosition : 0;

            var (filterDefinition, updateDefinition) = upd;

            return Collection.UpdateOneAsync(
                filterDefinition,
                updateDefinition.Set(x => x.Position, streamPosition),
                new UpdateOptions { IsUpsert = true },
                context.CancellationToken
            );
        }
    }

    protected abstract ValueTask<Operation<T>> GetUpdate(object evt, long? position);

    protected virtual ValueTask<Operation<T>> GetUpdate(IMessageConsumeContext context) {
        var streamPosition = context is MessageConsumeContext ctx ? (long?)ctx.StreamPosition : null;

        return GetUpdate(
            context.Message!,
            streamPosition
        );
    }

    protected Operation<T> UpdateOperation(BuildFilter<T> filter, BuildUpdate<T> update)
        => new UpdateOperation<T>(filter(Builders<T>.Filter), update(Builders<T>.Update));

    protected ValueTask<Operation<T>> UpdateOperationTask(
        BuildFilter<T> filter,
        BuildUpdate<T> update
    )
        => new(UpdateOperation(filter, update));

    protected Operation<T> UpdateOperation(string id, BuildUpdate<T> update)
        => UpdateOperation(filter => filter.Eq(x => x.Id, id), update);

    protected ValueTask<Operation<T>> UpdateOperationTask(string id, BuildUpdate<T> update)
        => new(UpdateOperation(id, update));

    protected static readonly ValueTask<Operation<T>> NoOp = new((Operation<T>)null!);
}

// ReSharper disable once UnusedTypeParameter
public abstract record Operation<T>;

public record UpdateOperation<T>
    (FilterDefinition<T> Filter, UpdateDefinition<T> Update) : Operation<T>;

public record OtherOperation<T>(Func<Task> Execute) : Operation<T>;

public record CollectionOperation<T>(
    Func<IMongoCollection<T>, CancellationToken, Task> Execute
) : Operation<T>;