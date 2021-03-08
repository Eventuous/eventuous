using System.Threading.Tasks;
using EventStore.Subscriptions;
using Eventuous.Projections.MongoDB.Tools;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Eventuous.Projections.MongoDB {
    [PublicAPI]
    public abstract class MongoProjection<T> : IEventHandler
        where T : ProjectedDocument {
        readonly ILogger?            _log;
        readonly IMongoCollection<T> _collection;

        protected MongoProjection(IMongoDatabase database, string subscriptionGroup, ILoggerFactory loggerFactory) {
            var log = loggerFactory.CreateLogger(GetType());
            _log              = log.IsEnabled(LogLevel.Debug) ? log : null;
            SubscriptionGroup = subscriptionGroup;
            _collection       = database.GetDocumentCollection<T>();
        }

        public string SubscriptionGroup { get; }

        public async Task HandleEvent(object evt, long? position) {
            var update = await GetUpdate(evt);

            if (update == null) {
                _log?.LogDebug("No handler for {Event}", evt.GetType().Name);
                return;
            }

            update.Update.Set(x => x.Position, position);

            _log?.LogDebug("Projecting {Event}", evt);
            await _collection.UpdateOneAsync(update.Filter, update.Update, new UpdateOptions {IsUpsert = true});
        }

        protected abstract ValueTask<UpdateOperation<T>> GetUpdate(object evt);

        protected UpdateOperation<T> Operation(BuildFilter<T> filter, BuildUpdate<T> update)
            => new(filter(Builders<T>.Filter), update(Builders<T>.Update));

        protected ValueTask<UpdateOperation<T>> OperationTask(BuildFilter<T> filter, BuildUpdate<T> update)
            => new(Operation(filter, update));

        protected UpdateOperation<T> Operation(string id, BuildUpdate<T> update)
            => Operation(filter => filter.Eq(x => x.Id, id), update);

        protected ValueTask<UpdateOperation<T>> OperationTask(string id, BuildUpdate<T> update)
            => new(Operation(id, update));

        protected ValueTask<UpdateOperation<T>> NoOp => new((UpdateOperation<T>) null!);
    }

    public record UpdateOperation<T>(FilterDefinition<T> Filter, UpdateDefinition<T> Update);

    public delegate UpdateDefinition<T> BuildUpdate<T>(UpdateDefinitionBuilder<T> update);

    public delegate FilterDefinition<T> BuildFilter<T>(FilterDefinitionBuilder<T> filter);
}