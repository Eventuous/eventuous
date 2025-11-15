// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Linq.Expressions;

namespace Eventuous.Projections.MongoDB.Tools;

[PublicAPI]
public static class MongoDatabaseExtensions {
    extension(IMongoDatabase database) {
        public Task<bool> DocumentExists<T>(string id, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().DocumentExists(id, cancellationToken);

        public Task<T?> LoadDocument<T>(string id, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().LoadDocument(id, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                       id,
                Expression<Func<T, TResult>> projection,
                CancellationToken            cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().LoadDocumentAs(id, projection, cancellationToken);

        public Task<List<T>> LoadDocuments<T>(IEnumerable<string> ids, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().LoadDocuments(ids, cancellationToken);

        public Task<List<TResult>> LoadDocumentsAs<T, TResult>(
                IEnumerable<string>          ids,
                Expression<Func<T, TResult>> projection,
                CancellationToken            cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().LoadDocumentsAs(ids, projection, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                           id,
                ProjectionDefinition<T, TResult> projection,
                CancellationToken                cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().LoadDocumentAs(id, projection, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                                                        id,
                Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T>> projection,
                CancellationToken                                             cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().LoadDocumentAs<T, TResult>(id, projection, cancellationToken);

        public Task StoreDocument<T>(T document, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().ReplaceDocument(document, cancellationToken);

        public Task ReplaceDocument<T>(T document, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().ReplaceDocument(document, cancellationToken);

        public Task<ReplaceOneResult> ReplaceDocument<T>(
                T                       document,
                Action<ReplaceOptions>? configure,
                CancellationToken       cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().ReplaceDocument(document, configure, cancellationToken);

        public Task UpdateDocument<T>(
                FilterDefinition<T>    filter,
                UpdateDefinition<T>    update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(filter, update, configure, cancellationToken);

        public Task UpdateDocument<T>(
                BuildFilter<T>         filter,
                BuildUpdate<T>         update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(filter, update, configure, cancellationToken);

        public Task UpdateDocument<T>(
                FilterDefinition<T> filter,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(filter, update, cancellationToken);

        public Task UpdateDocument<T>(
                BuildFilter<T>    filter,
                BuildUpdate<T>    update,
                CancellationToken cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(filter, update, cancellationToken);

        public Task UpdateDocument<T>(
                string                id,
                BuildUpdate<T>        update,
                Action<UpdateOptions> configure,
                CancellationToken     cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(id, update, configure, cancellationToken);

        public Task UpdateDocument<T>(string id, BuildUpdate<T> update, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(id, update, cancellationToken);

        public Task UpdateDocument<T>(string id, UpdateDefinition<T> update, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().UpdateDocument(id, update, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                BuildFilter<T>    filter,
                BuildUpdate<T>    update,
                CancellationToken cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateManyDocuments(filter, update, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                FilterDefinition<T> filter,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateManyDocuments(filter, update, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                BuildFilter<T>        filter,
                BuildUpdate<T>        update,
                Action<UpdateOptions> configure,
                CancellationToken     cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateManyDocuments(filter, update, configure, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                FilterDefinition<T>   filter,
                UpdateDefinition<T>   update,
                Action<UpdateOptions> configure,
                CancellationToken     cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().UpdateManyDocuments(filter, update, configure, cancellationToken);

        public Task<bool> DeleteDocument<T>(string id, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().DeleteDocument(id, cancellationToken);

        public Task<long> DeleteManyDocuments<T>(BuildFilter<T> filter, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().DeleteManyDocuments(filter, cancellationToken);

        public Task<long> DeleteManyDocuments<T>(FilterDefinition<T> filter, CancellationToken cancellationToken = default) where T : Document
            => database.GetDocumentCollection<T>().DeleteManyDocuments(filter, cancellationToken);

        public Task<long> BulkUpdateDocuments<T>(
                IEnumerable<T>           documents,
                BuildBulkFilter<T>       filter,
                BuildBulkUpdate<T>       update,
                Action<BulkWriteOptions> configure,
                CancellationToken        cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().BulkUpdateDocuments(documents, filter, update, configure, cancellationToken);

        public Task<long> BulkUpdateDocuments<T>(
                IEnumerable<T>     documents,
                BuildBulkFilter<T> filter,
                BuildBulkUpdate<T> update,
                CancellationToken  cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>().BulkUpdateDocuments(documents, filter, update, cancellationToken);

        public Task<bool> DocumentExists<T>(
                string              id,
                MongoCollectionName collectionName,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).DocumentExists(id, cancellationToken);

        public Task<T?> LoadDocument<T>(
                string              id,
                MongoCollectionName collectionName,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).LoadDocument(id, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                       id,
                Expression<Func<T, TResult>> projection,
                MongoCollectionName          collectionName,
                CancellationToken            cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).LoadDocumentAs(id, projection, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                           id,
                ProjectionDefinition<T, TResult> projection,
                MongoCollectionName              collectionName,
                CancellationToken                cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).LoadDocumentAs(id, projection, cancellationToken);

        public Task<TResult?> LoadDocumentAs<T, TResult>(
                string                                                        id,
                Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T>> projection,
                MongoCollectionName                                           collectionName,
                CancellationToken                                             cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName)
                .LoadDocumentAs<T, TResult>(id, projection, cancellationToken);

        public Task UpdateDocument<T>(
                string              id,
                MongoCollectionName collectionName,
                BuildUpdate<T>      update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).UpdateDocument(id, update, cancellationToken);

        public Task UpdateDocument<T>(
                string              id,
                MongoCollectionName collectionName,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).UpdateDocument(id, update, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                MongoCollectionName collectionName,
                BuildFilter<T>      filter,
                BuildUpdate<T>      update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).UpdateManyDocuments(filter, update, cancellationToken);

        public Task<long> UpdateManyDocuments<T>(
                MongoCollectionName collectionName,
                FilterDefinition<T> filter,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).UpdateManyDocuments(filter, update, cancellationToken);

        public Task<bool> DeleteDocument<T>(
                string              id,
                MongoCollectionName collectionName,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).DeleteDocument(id, cancellationToken);

        public Task<long> DeleteManyDocuments<T>(
                MongoCollectionName collectionName,
                BuildFilter<T>      filter,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).DeleteManyDocuments(filter, cancellationToken);

        public Task<long> DeleteManyDocuments<T>(
                MongoCollectionName collectionName,
                FilterDefinition<T> filter,
                CancellationToken   cancellationToken = default
            ) where T : Document
            => database.GetDocumentCollection<T>(collectionName).DeleteManyDocuments(filter, cancellationToken);

        public IQueryable<T> AsQueryable<T>(MongoCollectionName collectionName, Action<AggregateOptions>? configure = null)
            where T : Document {
            var options = new AggregateOptions();
            configure?.Invoke(options);

            return database.GetDocumentCollection<T>(collectionName).AsQueryable(options);
        }

        public IQueryable<T> AsQueryable<T>(Action<AggregateOptions>? configure = null) where T : Document {
            var options = new AggregateOptions();
            configure?.Invoke(options);

            return database.GetDocumentCollection<T>().AsQueryable(options);
        }

        public Task<string> CreateDocumentIndex<T>(BuildIndex<T> index, Action<CreateIndexOptions>? configure = null) where T : Document
            => database.GetDocumentCollection<T>().CreateDocumentIndex(index, configure);
    }
}
