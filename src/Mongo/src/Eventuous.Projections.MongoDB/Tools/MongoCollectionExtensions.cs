using System.Linq.Expressions;
using Eventuous.Subscriptions.Tools;
using static System.String;

namespace Eventuous.Projections.MongoDB.Tools; 

[PublicAPI]
public static class MongoCollectionExtensions {
    public static IMongoCollection<T> GetDocumentCollection<T>(
        this IMongoDatabase  database,
        MongoCollectionName? collectionName = null
    )
        where T : Document
        => GetDocumentCollection<T>(database, collectionName ?? MongoCollectionName.For<T>(), null);

    public static IMongoCollection<T> GetDocumentCollection<T>(
        this IMongoDatabase     database,
        MongoCollectionSettings settings
    ) where T : Document
        => GetDocumentCollection<T>(database, MongoCollectionName.For<T>(), settings);

    public static IMongoCollection<T> GetDocumentCollection<T>(
        this IMongoDatabase      database,
        MongoCollectionName?     collectionName,
        MongoCollectionSettings? settings
    ) where T : Document
        => database.GetCollection<T>(
            collectionName ?? MongoCollectionName.For<T>(),
            settings
        );

    public static Task<bool> DocumentExists<T>(
        this IMongoCollection<T> collection,
        string                   id,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

        return collection
            .Find(x => x.Id == id)
            .AnyAsync(cancellationToken);
    }

    public static Task<T?> LoadDocument<T>(
        this IMongoCollection<T> collection,
        string                   id,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

        return collection
            .Find(x => x.Id == id)
            .Limit(1)
            .SingleOrDefaultAsync(cancellationToken)!;
    }

    public static Task<TResult?> LoadDocumentAs<T, TResult>(
        this IMongoCollection<T>     collection,
        string                       id,
        Expression<Func<T, TResult>> projection,
        CancellationToken            cancellationToken = default
    ) where T : Document {
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

        if (projection == null) throw new ArgumentNullException(nameof(projection));

        return collection
            .Find(x => x.Id == id)
            .Limit(1)
            .Project(projection)
            .SingleOrDefaultAsync(cancellationToken)!;
    }

    public static Task<List<T>> LoadDocuments<T>(
        this IMongoCollection<T> collection,
        IEnumerable<string>      ids,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        var idsList = ids.ToList();

        if (ids == null || idsList.Count == 0 || idsList.Any(IsNullOrWhiteSpace))
            throw new ArgumentException(
                "Document ids collection cannot be empty or contain empty values",
                nameof(ids)
            );

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
            throw new ArgumentException(
                "Document ids collection cannot be empty or contain empty values",
                nameof(ids)
            );

        if (projection == null) throw new ArgumentNullException(nameof(projection), "Projection must be specified");

        return collection
            .Find(Builders<T>.Filter.In(x => x.Id, idsList))
            .Project(projection)
            .ToListAsync(cancellationToken);
    }

    public static Task<TResult?> LoadDocumentAs<T, TResult>(
        this IMongoCollection<T>         collection,
        string                           id,
        ProjectionDefinition<T, TResult> projection,
        CancellationToken                cancellationToken = default
    ) where T : Document {
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

        if (projection == null) throw new ArgumentNullException(nameof(projection));

        return collection
            .Find(x => x.Id == id)
            .Limit(1)
            .Project(projection)
            .SingleOrDefaultAsync(cancellationToken)!;
    }

    public static Task<TResult?> LoadDocumentAs<T, TResult>(
        this IMongoCollection<T>                                      collection,
        string                                                        id,
        Func<ProjectionDefinitionBuilder<T>, ProjectionDefinition<T>> projection,
        CancellationToken                                             cancellationToken = default
    ) where T : Document
        => collection.LoadDocumentAs<T, TResult>(id, projection(Builders<T>.Projection), cancellationToken);

    public static async Task<ReplaceOneResult> ReplaceDocument<T>(
        this IMongoCollection<T> collection,
        T                        document,
        Action<ReplaceOptions>?  configure,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (document == null) throw new ArgumentNullException(nameof(document), "Document cannot be null.");

        var options = new ReplaceOptions { IsUpsert = true };

        configure?.Invoke(options);

        return await collection.ReplaceOneAsync(
            x => x.Id == document.Id,
            document,
            options,
            cancellationToken
        ).NoContext();
    }

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
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

        var result = await collection.DeleteOneAsync(x => x.Id == id, cancellationToken).NoContext();

        return result.DeletedCount == 1;
    }

    public static async Task<long> DeleteManyDocuments<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T>      filter,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (filter == null) throw new ArgumentNullException(nameof(filter));

        var result = await collection.DeleteManyAsync(filter, cancellationToken).NoContext();

        return result.DeletedCount;
    }

    public static Task<long> DeleteManyDocuments<T>(
        this IMongoCollection<T> collection,
        BuildFilter<T>           filter,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.DeleteManyDocuments(filter(Builders<T>.Filter), cancellationToken);

    public static async Task<long> BulkUpdateDocuments<T>(
        this IMongoCollection<T>  collection,
        IEnumerable<T>            documents,
        BuildBulkFilter<T>        filter,
        BuildBulkUpdate<T>        update,
        Action<BulkWriteOptions>? configure,
        CancellationToken         cancellationToken = default
    ) where T : Document {
        var options = new BulkWriteOptions();

        configure?.Invoke(options);

        var models = documents.Select(
            document => new UpdateOneModel<T>(
                filter(document, Builders<T>.Filter),
                update(document, Builders<T>.Update)
            )
        );

        var result = await collection.BulkWriteAsync(models, options, cancellationToken).NoContext();

        return result.ModifiedCount;
    }

    public static async Task<BulkWriteResult> BulkWriteDocuments<T>(
        this IMongoCollection<T>  collection,
        IEnumerable<T>            documents,
        Func<T, WriteModel<T>>    write,
        Action<BulkWriteOptions>? configure,
        CancellationToken         cancellationToken = default
    ) where T : Document {
        var options = new BulkWriteOptions();

        configure?.Invoke(options);

        return await collection.BulkWriteAsync(documents.Select(write), options, cancellationToken).NoContext();
    }

    public static Task<long> BulkUpdateDocuments<T>(
        this IMongoCollection<T> collection,
        IEnumerable<T>           documents,
        BuildBulkFilter<T>       filter,
        BuildBulkUpdate<T>       update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.BulkUpdateDocuments(documents, filter, update, null, cancellationToken);

    public static Task<string> CreateDocumentIndex<T>(
        this IMongoCollection<T>    collection,
        BuildIndex<T>               index,
        Action<CreateIndexOptions>? configure = null
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
        this IMongoCollection<T>    collection,
        BuildIndex<T>               index,
        Action<CreateIndexOptions>? configure,
        CancellationToken           cancellationToken
    ) where T : Document {
        var options = new CreateIndexOptions();

        configure?.Invoke(options);

        try {
            return await CreateIndex().NoContext();
        }
        catch (MongoCommandException ex) when (ex.Message.Contains("already exists")) {
            // Ignore
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

    public static async Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T>      filter,
        UpdateDefinition<T>      update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        var options = new UpdateOptions { IsUpsert = true };

        configure?.Invoke(options);

        await collection.UpdateOneAsync(
            filter,
            update,
            options,
            cancellationToken
        ).NoContext();
    }

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        BuildFilter<T>           filter,
        BuildUpdate<T>           update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(
            filter(Builders<T>.Filter),
            update(Builders<T>.Update),
            configure,
            cancellationToken
        );

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T>      filter,
        UpdateDefinition<T>      update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(filter, update, null, cancellationToken);

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        BuildFilter<T>           filter,
        BuildUpdate<T>           update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(
            filter(Builders<T>.Filter),
            update(Builders<T>.Update),
            null,
            cancellationToken
        );

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        string                   id,
        UpdateDefinition<T>      update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (IsNullOrWhiteSpace(id))
            throw new ArgumentException("Document Id cannot be null or whitespace.", nameof(id));

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
        this IMongoCollection<T> collection,
        string                   id,
        BuildUpdate<T>           update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(
            id,
            update(Builders<T>.Update),
            configure,
            cancellationToken
        );

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        string                   id,
        UpdateDefinition<T>      update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(id, update, null, cancellationToken);

    public static Task UpdateDocument<T>(
        this IMongoCollection<T> collection,
        string                   id,
        BuildUpdate<T>           update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateDocument(id, update, null, cancellationToken);

    public static async Task<long> UpdateManyDocuments<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T>      filter,
        UpdateDefinition<T>      update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document {
        if (filter == null) throw new ArgumentNullException(nameof(filter));
        if (update == null) throw new ArgumentNullException(nameof(update));

        var options = new UpdateOptions { IsUpsert = true };

        configure?.Invoke(options);

        var result = await collection.UpdateManyAsync(filter, update, options, cancellationToken).NoContext();

        return result.ModifiedCount;
    }

    public static Task<long> UpdateManyDocuments<T>(
        this IMongoCollection<T> collection,
        BuildFilter<T>           filter,
        BuildUpdate<T>           update,
        Action<UpdateOptions>?   configure,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateManyDocuments(
            filter(Builders<T>.Filter),
            update(Builders<T>.Update),
            configure,
            cancellationToken
        );

    public static Task<long> UpdateManyDocuments<T>(
        this IMongoCollection<T> collection,
        FilterDefinition<T>      filter,
        UpdateDefinition<T>      update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateManyDocuments(filter, update, null, cancellationToken);

    public static Task<long> UpdateManyDocuments<T>(
        this IMongoCollection<T> collection,
        BuildFilter<T>           filter,
        BuildUpdate<T>           update,
        CancellationToken        cancellationToken = default
    ) where T : Document
        => collection.UpdateManyDocuments(
            filter(Builders<T>.Filter),
            update(Builders<T>.Update),
            null,
            cancellationToken
        );
}