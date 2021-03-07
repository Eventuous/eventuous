using System;
using System.Threading.Tasks;
using EventStore.Subscriptions;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using MongoTools;

namespace CoreLib.MongoDB {
    public abstract class MongoProjection<T> : IEventHandler
        where T : Document {
        readonly ILogger             _log;
        readonly IMongoCollection<T> _collection;

        protected MongoProjection(IMongoDatabase database, string subscriptionGroup, ILoggerFactory loggerFactory) {
            _log              = loggerFactory.CreateLogger(GetType());
            SubscriptionGroup = subscriptionGroup;
            _collection       = database.GetDocumentCollection<T>();
        }

        public string SubscriptionGroup { get; }

        public async Task HandleEvent(object evt, long? position) {
            var update = await GetUpdate(evt);

            if (update == null) {
                _log.LogDebug("No handler for {Event}", evt.GetType().Name);
                return;
            }

            // var finalUpdate = update.Update.Set(x => x.Position, position);

            _log.LogDebug("Projecting {Event}", evt);
            await _collection.UpdateOneAsync(update.Filter, update.Update, new UpdateOptions {IsUpsert = true});
        }

        protected abstract ValueTask<UpdateOperation<T>> GetUpdate(object evt);

        protected UpdateOperation<T> Operation(
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update
        )
            => new(filter(Builders<T>.Filter), update(Builders<T>.Update));

        protected ValueTask<UpdateOperation<T>> OperationTask(
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update
        )
            => new(Operation(filter, update));

        protected UpdateOperation<T> Operation(
            string                                                id,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update
        )
            => Operation(filter => filter.Eq(x => x.Id, id), update);

        protected ValueTask<UpdateOperation<T>> OperationTask(
            string                                                id,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update
        )
            => new(Operation(id, update));

        protected ValueTask<UpdateOperation<T>> NoOp => new((UpdateOperation<T>) null);
    }

    public record UpdateOperation<T>(FilterDefinition<T> Filter, UpdateDefinition<T> Update);
}