using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using MongoDB.Driver;
using static System.String;

namespace Eventuous.Projections.MongoDB.Tools {
    public static class MongoCollectionExtensions {
        public static IMongoCollection<T> GetDocumentCollection<T>(this IMongoDatabase database, MongoCollectionName collectionName = null)
            where T : Document
            => GetDocumentCollection<T>(database, collectionName ?? MongoCollectionName.For<T>(), null);

        public static IMongoCollection<T> GetDocumentCollection<T>(
            this IMongoDatabase     database,
            MongoCollectionSettings settings
        ) where T : Document
            => GetDocumentCollection<T>(database, MongoCollectionName.For<T>(), settings);

        public static IMongoCollection<T> GetDocumentCollection<T>(
            this IMongoDatabase     database,
            MongoCollectionName     collectionName,
            MongoCollectionSettings settings
        ) where T : Document
            => database.GetCollection<T>(collectionName == null ? MongoCollectionName.For<T>() : collectionName, settings);

        public static Task<bool> DocumentExists<T>(
            this IMongoCollection<T> collection,
            string                   id,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            return collection
                .Find(x => x.Id == id)
                .AnyAsync(cancellationToken);
        }

        public static Task<T> LoadDocument<T>(
            this IMongoCollection<T> collection,
            string                   id,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public static Task<TResult> LoadDocumentAs<T, TResult>(
            this IMongoCollection<T>     collection,
            string                       id,
            Expression<Func<T, TResult>> projection,
            CancellationToken            cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            if (projection == null) throw new ArgumentNullException(nameof(projection));

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .Project(projection)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public static Task<List<T>> LoadDocuments<T>(
            this IMongoCollection<T> collection,
            IEnumerable<string>      ids,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            var idsList = ids.ToList();

            if (ids == null || idsList.Count == 0 || idsList.Any(IsNullOrWhiteSpace))
                throw new ArgumentException("Document ids collection cannot be empty or contain empty values", nameof(ids));

            return collection
                .Find(Builders<T>.Filter.In(x => x.Id, idsList))
                .ToListAsync(cancellationToken);
        }

        public static Task<List<TResult>> LoadDocumentsAs<T, TResult>(
            this IMongoCollection<T>     collection,
            IEnumerable<string>          ids,
            Expression<Func<T, TResult>> projection,
            CancellationToken            cancellationToken = default
        ) where T : Document {
            var idsList = ids.ToList();

            if (ids == null || idsList.Count == 0 || idsList.Any(IsNullOrWhiteSpace))
                throw new ArgumentException("Document ids collection cannot be empty or contain empty values", nameof(ids));

            if (projection == null) throw new ArgumentNullException(nameof(projection), "Projection must be specified");

            return collection
                .Find(Builders<T>.Filter.In(x => x.Id, idsList))
                .Project(projection)
                .ToListAsync(cancellationToken);
        }

        public static Task<TResult> LoadDocumentAs<T, TResult>(
            this IMongoCollection<T>         collection,
            string                           id,
            ProjectionDefinition<T, TResult> projection,
            CancellationToken                cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            if (projection == null) throw new ArgumentNullException(nameof(projection));

            return collection
                .Find(x => x.Id == id)
                .Limit(1)
                .Project(projection)
                .SingleOrDefaultAsync(cancellationToken);
        }

        public static Task<TResult> LoadDocumentAs<T, TResult>(
            this IMongoCollection<T>                                      collection,
            string                                                        id,
            Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T>> projection,
            CancellationToken                                             cancellationToken = default
        ) where T : Document
            => collection.LoadDocumentAs<T, TResult>(id, projection(Builders<T>.Projection), cancellationToken);

        /// <summary>
        /// Replaces the document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static async Task<ReplaceOneResult> ReplaceDocument<T>(
            this IMongoCollection<T> collection,
            T                        document,
            Action<ReplaceOptions>   configure,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (document == null) throw new ArgumentNullException(nameof(document), "Document cannot be null.");

            var options = new ReplaceOptions {IsUpsert = true};

            configure?.Invoke(options);

            return await collection.ReplaceOneAsync(
                x => x.Id == document.Id,
                document,
                options,
                cancellationToken
            );
        }

        /// <summary>
        /// Replaces the document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static Task ReplaceDocument<T>(
            this IMongoCollection<T> collection,
            T                        document,
            CancellationToken        cancellationToken = default
        ) where T : Document
            => collection.ReplaceDocument(document, null, cancellationToken);

        public static async Task<bool> DeleteDocument<T>(
            this IMongoCollection<T> collection,
            string                   id,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            var result = await collection.DeleteOneAsync(x => x.Id == id, cancellationToken);

            return result.DeletedCount == 1;
        }

        public static async Task<long> DeleteManyDocuments<T>(
            this IMongoCollection<T> collection,
            FilterDefinition<T>      filter,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (filter == null) throw new ArgumentNullException(nameof(filter));

            var result = await collection.DeleteManyAsync(filter, cancellationToken);

            return result.DeletedCount;
        }

        public static Task<long> DeleteManyDocuments<T>(
            this IMongoCollection<T>                              collection,
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.DeleteManyDocuments(filter(Builders<T>.Filter), cancellationToken);

        public static async Task<long> BulkUpdateDocuments<T>(
            this IMongoCollection<T>                                 collection,
            IEnumerable<T>                                           documents,
            Func<T, FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<T, UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            Action<BulkWriteOptions>                                 configure,
            CancellationToken                                        cancellationToken = default
        ) where T : Document {
            var options = new BulkWriteOptions();

            configure(options);

            var models = documents.Select(
                document => new UpdateOneModel<T>(
                    filter(document, Builders<T>.Filter),
                    update(document, Builders<T>.Update)
                )
            );

            var result = await collection.BulkWriteAsync(models, options, cancellationToken);

            return result.ModifiedCount;
        }

        public static async Task<BulkWriteResult> BulkWriteDocuments<T>(
            this IMongoCollection<T> collection,
            IEnumerable<T>           documents,
            Func<T, WriteModel<T>>   write,
            Action<BulkWriteOptions> configure,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            var options = new BulkWriteOptions();

            configure(options);

            return await collection.BulkWriteAsync(documents.Select(write), options, cancellationToken);
        }

        public static Task<long> BulkUpdateDocuments<T>(
            this IMongoCollection<T>                                 collection,
            IEnumerable<T>                                           documents,
            Func<T, FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<T, UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            CancellationToken                                        cancellationToken = default
        ) where T : Document
            => collection.BulkUpdateDocuments(documents, filter, update, null, cancellationToken);

        public static Task<string> CreateDocumentIndex<T>(
            this IMongoCollection<T>                                    collection,
            Func<IndexKeysDefinitionBuilder<T>, IndexKeysDefinition<T>> index,
            Action<CreateIndexOptions>                                  configure = null
        ) where T : Document {
            var options = new CreateIndexOptions();

            configure?.Invoke(options);

            return collection.Indexes.CreateOneAsync(
                new CreateIndexModel<T>(
                    index(Builders<T>.IndexKeys),
                    options
                )
            );
        }

        public static async Task<string> CreateDocumentIndex<T>(
            this IMongoCollection<T>                                    collection,
            Func<IndexKeysDefinitionBuilder<T>, IndexKeysDefinition<T>> index,
            Action<CreateIndexOptions>                                  configure,
            CancellationToken                                           cancellationToken
        ) where T : Document {
            var options = new CreateIndexOptions();

            configure?.Invoke(options);

            try {
                return await CreateIndex();
            }
            catch (MongoCommandException ex) when (ex.Message.Contains("already exists")) {
                // Ignore, but the user should decide
            }

            return Empty;

            Task<string> CreateIndex()
                => collection.Indexes.CreateOneAsync(
                    new CreateIndexModel<T>(
                        index(Builders<T>.IndexKeys),
                        options
                    ),
                    cancellationToken: cancellationToken
                );
        }

        #region . Update .

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document is found.
        /// </summary>
        public static async Task UpdateDocument<T>(
            this IMongoCollection<T> collection,
            FilterDefinition<T>      filter,
            UpdateDefinition<T>      update,
            Action<UpdateOptions>    configure,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            var options = new UpdateOptions {IsUpsert = true};

            configure?.Invoke(options);

            var result = await collection.UpdateOneAsync(
                filter,
                update,
                options,
                cancellationToken
            );
        }

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T>                              collection,
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            Action<UpdateOptions>                                 configure,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(
                filter(Builders<T>.Filter),
                update(Builders<T>.Update),
                configure,
                cancellationToken
            );

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T> collection,
            FilterDefinition<T>      filter,
            UpdateDefinition<T>      update,
            CancellationToken        cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(filter, update, null, cancellationToken);

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T>                              collection,
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(
                filter(Builders<T>.Filter),
                update(Builders<T>.Update),
                null,
                cancellationToken
            );

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T> collection,
            string                   id,
            UpdateDefinition<T>      update,
            Action<UpdateOptions>    configure,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (IsNullOrWhiteSpace(id)) throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

            return collection.UpdateDocument(
                Builders<T>.Filter.Eq(x => x.Id, id),
                update,
                configure,
                cancellationToken
            );
        }

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T>                              collection,
            string                                                id,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            Action<UpdateOptions>                                 configure,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(
                id,
                update(Builders<T>.Update),
                configure,
                cancellationToken
            );

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T> collection,
            string                   id,
            UpdateDefinition<T>      update,
            CancellationToken        cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(id, update, null, cancellationToken);

        /// <summary>
        /// Updates a document and by default inserts a new one if no matching document by id is found.
        /// </summary>
        public static Task UpdateDocument<T>(
            this IMongoCollection<T>                              collection,
            string                                                id,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateDocument(id, update, null, cancellationToken);

        #endregion

        #region . UpdateMany .

        /// <summary>
        /// Updates documents and by default inserts new ones if no matching documents are found.
        /// </summary>
        public static async Task<long> UpdateManyDocuments<T>(
            this IMongoCollection<T> collection,
            FilterDefinition<T>      filter,
            UpdateDefinition<T>      update,
            Action<UpdateOptions>    configure,
            CancellationToken        cancellationToken = default
        ) where T : Document {
            if (filter == null) throw new ArgumentNullException(nameof(filter));
            if (update == null) throw new ArgumentNullException(nameof(update));

            var options = new UpdateOptions {IsUpsert = true};

            configure?.Invoke(options);

            var result = await collection.UpdateManyAsync(filter, update, options, cancellationToken);

            return result.ModifiedCount;
        }

        /// <summary>
        /// Updates documents and by default inserts new ones if no matching documents are found.
        /// </summary>
        public static Task<long> UpdateManyDocuments<T>(
            this IMongoCollection<T>                              collection,
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            Action<UpdateOptions>                                 configure,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateManyDocuments(filter(Builders<T>.Filter), update(Builders<T>.Update), configure, cancellationToken);

        /// <summary>
        /// Updates documents and by default inserts new ones if no matching documents are found.
        /// </summary>
        public static Task<long> UpdateManyDocuments<T>(
            this IMongoCollection<T> collection,
            FilterDefinition<T>      filter,
            UpdateDefinition<T>      update,
            CancellationToken        cancellationToken = default
        ) where T : Document
            => collection.UpdateManyDocuments(filter, update, null, cancellationToken);

        /// <summary>
        /// Updates documents and by default inserts new ones if no matching documents by filter are found.
        /// </summary>
        public static Task<long> UpdateManyDocuments<T>(
            this IMongoCollection<T>                              collection,
            Func<FilterDefinitionBuilder<T>, FilterDefinition<T>> filter,
            Func<UpdateDefinitionBuilder<T>, UpdateDefinition<T>> update,
            CancellationToken                                     cancellationToken = default
        ) where T : Document
            => collection.UpdateManyDocuments(filter(Builders<T>.Filter), update(Builders<T>.Update), null, cancellationToken);

        #endregion
    }
}
