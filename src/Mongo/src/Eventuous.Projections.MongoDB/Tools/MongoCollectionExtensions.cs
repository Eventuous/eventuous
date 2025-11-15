// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Linq.Expressions;
using static System.String;

namespace Eventuous.Projections.MongoDB.Tools;

[PublicAPI]
public static class MongoCollectionExtensions {
    extension(IMongoDatabase database) {
        public IMongoCollection<T> GetDocumentCollection<T>(MongoCollectionName? collectionName = null) where T : Document
            => GetDocumentCollection<T>(database, collectionName ?? MongoCollectionName.For<T>(), null);

        public IMongoCollection<T> GetDocumentCollection<T>(MongoCollectionSettings settings) where T : Document
            => GetDocumentCollection<T>(database, MongoCollectionName.For<T>(), settings);

        public IMongoCollection<T> GetDocumentCollection<T>(MongoCollectionName? collectionName, MongoCollectionSettings? settings)
            where T : Document
            => database.GetCollection<T>(collectionName ?? MongoCollectionName.For<T>(), settings);
    }

    extension<T>(IMongoCollection<T> collection) where T : Document {
        public Task<bool> DocumentExists(string id, CancellationToken cancellationToken = default) {
            ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document Id cannot be null or whitespace.");

            return collection
                .Find(x => x.Id == id)
                .AnyAsync(cancellationToken);
        }

        public Task<T?> LoadDocument(string id, CancellationToken cancellationToken = default) {
            ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document Id cannot be null or whitespace.");

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .SingleOrDefaultAsync(cancellationToken)!;
        }

        public Task<TResult?> LoadDocumentAs<TResult>(
                string                       id,
                Expression<Func<T, TResult>> projection,
                CancellationToken            cancellationToken = default
            ) {
            ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document Id cannot be null or whitespace.");
            ArgumentNullException.ThrowIfNull(projection);

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .Project(projection)
                .SingleOrDefaultAsync(cancellationToken)!;
        }

        public Task<List<T>> LoadDocuments(IEnumerable<string> ids, CancellationToken cancellationToken = default) {
            var idsList = ids.ToList();

            if (ids == null || idsList.Count == 0 || idsList.Any(IsNullOrWhiteSpace))
                throw new ArgumentException("Document ids collection cannot be empty or contain empty values", nameof(ids));

            return collection
                .Find(Builders<T>.Filter.In(x => x.Id, idsList))
                .ToListAsync(cancellationToken);
        }

        public Task<List<TResult>> LoadDocumentsAs<TResult>(
                IEnumerable<string>          ids,
                Expression<Func<T, TResult>> projection,
                CancellationToken            cancellationToken = default
            ) {
            var idsList = ids.ToList();

            if (ids == null || idsList.Count == 0 || idsList.Any(IsNullOrWhiteSpace))
                throw new ArgumentException("Document ids collection cannot be empty or contain empty values", nameof(ids));

            ArgumentNullException.ThrowIfNull(projection, "Projection must be specified");

            return collection
                .Find(Builders<T>.Filter.In(x => x.Id, idsList))
                .Project(projection)
                .ToListAsync(cancellationToken);
        }

        public Task<TResult?> LoadDocumentAs<TResult>(
                string                           id,
                ProjectionDefinition<T, TResult> projection,
                CancellationToken                cancellationToken = default
            ) {
            ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document Id cannot be null or whitespace.");
            ArgumentNullException.ThrowIfNull(projection);

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .Project(projection)
                .SingleOrDefaultAsync(cancellationToken)!;
        }

        public Task<TResult?> LoadDocumentAs<TResult>(
                string                                                        id,
                Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T>> projection,
                CancellationToken                                             cancellationToken = default
            )
            => collection.LoadDocumentAs<T, TResult>(id, projection(Builders<T>.Projection), cancellationToken);

        public async Task<ReplaceOneResult> ReplaceDocument(
                T                       document,
                Action<ReplaceOptions>? configure,
                CancellationToken       cancellationToken = default
            ) {
            ArgumentNullException.ThrowIfNull(document, "Document cannot be null");

            var options = new ReplaceOptions { IsUpsert = true };

            configure?.Invoke(options);

            return await collection.ReplaceOneAsync(
                    x => x.Id == document.Id,
                    document,
                    options,
                    cancellationToken
                )
                .NoContext();
        }

        public Task ReplaceDocument(T document, CancellationToken cancellationToken = default) => collection.ReplaceDocument(document, null, cancellationToken);

        public async Task<bool> DeleteDocument(string id, CancellationToken cancellationToken = default) {
            ArgumentException.ThrowIfNullOrWhiteSpace(id, "Document Id cannot be null or whitespace.");

            var result = await collection.DeleteOneAsync(x => x.Id == id, cancellationToken).NoContext();

            return result.DeletedCount == 1;
        }

        public async Task<long> DeleteManyDocuments(FilterDefinition<T> filter, CancellationToken cancellationToken = default) {
            ArgumentNullException.ThrowIfNull(filter);

            var result = await collection.DeleteManyAsync(filter, cancellationToken).NoContext();

            return result.DeletedCount;
        }

        public Task<long> DeleteManyDocuments(BuildFilter<T> filter, CancellationToken cancellationToken = default) => collection.DeleteManyDocuments(filter(Builders<T>.Filter), cancellationToken);

        public async Task<long> BulkUpdateDocuments(
                IEnumerable<T>            documents,
                BuildBulkFilter<T>        filter,
                BuildBulkUpdate<T>        update,
                Action<BulkWriteOptions>? configure,
                CancellationToken         cancellationToken = default
            ) {
            var options = new BulkWriteOptions();

            configure?.Invoke(options);

            var models = documents.Select(document => new UpdateOneModel<T>(
                    filter(document, Builders<T>.Filter),
                    update(document, Builders<T>.Update)
                )
            );

            var result = await collection.BulkWriteAsync(models, options, cancellationToken).NoContext();

            return result.ModifiedCount;
        }

        public async Task<BulkWriteResult> BulkWriteDocuments(
                IEnumerable<T>            documents,
                Func<T, WriteModel<T>>    write,
                Action<BulkWriteOptions>? configure,
                CancellationToken         cancellationToken = default
            ) {
            var options = new BulkWriteOptions();

            configure?.Invoke(options);

            return await collection.BulkWriteAsync(documents.Select(write), options, cancellationToken).NoContext();
        }

        public Task<long> BulkUpdateDocuments(
                IEnumerable<T>     documents,
                BuildBulkFilter<T> filter,
                BuildBulkUpdate<T> update,
                CancellationToken  cancellationToken = default
            )
            => collection.BulkUpdateDocuments(documents, filter, update, null, cancellationToken);

        public Task<string> CreateDocumentIndex(BuildIndex<T> index, Action<CreateIndexOptions>? configure = null) {
            var options = new CreateIndexOptions();

            configure?.Invoke(options);

            return collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(index(Builders<T>.IndexKeys), options));
        }

        public async Task<string> CreateDocumentIndex(
                BuildIndex<T>               index,
                Action<CreateIndexOptions>? configure,
                CancellationToken           cancellationToken
            ) {
            var options = new CreateIndexOptions();

            configure?.Invoke(options);

            try {
                return await CreateIndex().NoContext();
            } catch (MongoCommandException ex) when (ex.Message.Contains("already exists")) {
                // Ignore
            }

            return Empty;

            Task<string> CreateIndex()
                => collection.Indexes.CreateOneAsync(new CreateIndexModel<T>(index(Builders<T>.IndexKeys), options), cancellationToken: cancellationToken);
        }

        public async Task UpdateDocument(
                FilterDefinition<T>    filter,
                UpdateDefinition<T>    update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            ) {
            var options = new UpdateOptions { IsUpsert = true };

            configure?.Invoke(options);

            await collection.UpdateOneAsync(filter, update, options, cancellationToken).NoContext();
        }

        public Task UpdateDocument(
                BuildFilter<T>         filter,
                BuildUpdate<T>         update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            )
            => collection.UpdateDocument(filter(Builders<T>.Filter), update(Builders<T>.Update), configure, cancellationToken);

        public Task UpdateDocument(
                FilterDefinition<T> filter,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            )
            => collection.UpdateDocument(filter, update, null, cancellationToken);

        public Task UpdateDocument(
                BuildFilter<T>    filter,
                BuildUpdate<T>    update,
                CancellationToken cancellationToken = default
            )
            => collection.UpdateDocument(filter(Builders<T>.Filter), update(Builders<T>.Update), null, cancellationToken);

        public Task UpdateDocument(
                string                 id,
                UpdateDefinition<T>    update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            )
            => IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id))
                : collection.UpdateDocument(Builders<T>.Filter.Eq(x => x.Id, id), update, configure, cancellationToken);

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public Task UpdateDocument(
                string                 id,
                BuildUpdate<T>         update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            )
            => collection.UpdateDocument(id, update(Builders<T>.Update), configure, cancellationToken);

        public Task UpdateDocument(
                string              id,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            )
            => collection.UpdateDocument(id, update, null, cancellationToken);

        public Task UpdateDocument(
                string            id,
                BuildUpdate<T>    update,
                CancellationToken cancellationToken = default
            )
            => collection.UpdateDocument(id, update, null, cancellationToken);

        public async Task<long> UpdateManyDocuments(
                FilterDefinition<T>    filter,
                UpdateDefinition<T>    update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            ) {
            ArgumentNullException.ThrowIfNull(filter);
            ArgumentNullException.ThrowIfNull(update);

            var options = new UpdateOptions { IsUpsert = true };

            configure?.Invoke(options);

            var result = await collection.UpdateManyAsync(filter, update, options, cancellationToken).NoContext();

            return result.ModifiedCount;
        }

        public Task<long> UpdateManyDocuments(
                BuildFilter<T>         filter,
                BuildUpdate<T>         update,
                Action<UpdateOptions>? configure,
                CancellationToken      cancellationToken = default
            )
            => collection.UpdateManyDocuments(filter(Builders<T>.Filter), update(Builders<T>.Update), configure, cancellationToken);

        public Task<long> UpdateManyDocuments(
                FilterDefinition<T> filter,
                UpdateDefinition<T> update,
                CancellationToken   cancellationToken = default
            )
            => collection.UpdateManyDocuments(filter, update, null, cancellationToken);

        public Task<long> UpdateManyDocuments(
                BuildFilter<T>    filter,
                BuildUpdate<T>    update,
                CancellationToken cancellationToken = default
            )
            => collection.UpdateManyDocuments(filter(Builders<T>.Filter), update(Builders<T>.Update), null, cancellationToken);
    }
}
