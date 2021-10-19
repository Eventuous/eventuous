using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Logging;

namespace Eventuous.Projections.MongoDB; 

[PublicAPI]
public abstract class MongoProjection<T> : IEventHandler
    where T : ProjectedDocument {

    protected IMongoCollection<T> Collection { get; }

    protected MongoProjection(IMongoDatabase database) {
        Collection     = Ensure.NotNull(database, nameof(database)).GetDocumentCollection<T>();
    }

    public void SetLogger(SubscriptionLog subscriptionLogger) => Log = subscriptionLogger;

    public SubscriptionLog? Log { get; set; }

    public async Task HandleEvent(ReceivedEvent evt, CancellationToken cancellationToken) {
        var updateTask = GetUpdate(evt);
        var update     = updateTask == NoOp ? null : await updateTask.NoContext();

        if (update == null) {
            Log?.Debug?.Invoke("No handler for {Event}", evt.Payload!.GetType().Name);
            return;
        }

        Log?.Debug?.Invoke("Projecting {Event}", evt.Payload!.GetType().Name);

        var task = update switch {
            OtherOperation<T> operation => operation.Execute(),
            CollectionOperation<T> col  => col.Execute(Collection, cancellationToken),
            UpdateOperation<T> upd      => ExecuteUpdate(upd),
            _                           => Task.CompletedTask
        };

        await task.NoContext();

        Task ExecuteUpdate(UpdateOperation<T> upd)
            => Collection.UpdateOneAsync(
                upd.Filter,
                upd.Update.Set(x => x.Position, (long)evt.StreamPosition),
                new UpdateOptions { IsUpsert = true },
                cancellationToken
            );
    }
    
    protected abstract ValueTask<Operation<T>> GetUpdate(object evt, long? position);

    protected virtual ValueTask<Operation<T>> GetUpdate(ReceivedEvent receivedEvent) {
        return GetUpdate(receivedEvent.Payload!, (long?)receivedEvent.StreamPosition);
    }

    protected Operation<T> UpdateOperation(BuildFilter<T> filter, BuildUpdate<T> update)
        => new UpdateOperation<T>(filter(Builders<T>.Filter), update(Builders<T>.Update));

    protected ValueTask<Operation<T>> UpdateOperationTask(BuildFilter<T> filter, BuildUpdate<T> update)
        => new(UpdateOperation(filter, update));

    protected Operation<T> UpdateOperation(string id, BuildUpdate<T> update)
        => UpdateOperation(filter => filter.Eq(x => x.Id, id), update);

    protected ValueTask<Operation<T>> UpdateOperationTask(string id, BuildUpdate<T> update)
        => new(UpdateOperation(id, update));

    protected static readonly ValueTask<Operation<T>> NoOp = new((Operation<T>) null!);
}

// ReSharper disable once UnusedTypeParameter
public abstract record Operation<T>;

public record UpdateOperation<T>(FilterDefinition<T> Filter, UpdateDefinition<T> Update) : Operation<T>;

public record OtherOperation<T>(Func<Task> Execute) : Operation<T>;

public record CollectionOperation<T>(Func<IMongoCollection<T>, CancellationToken, Task> Execute) : Operation<T>;