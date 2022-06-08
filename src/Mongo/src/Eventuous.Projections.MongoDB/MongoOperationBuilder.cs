// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

public class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public UpdateOneBuilder  UpdateOne  => new();
    public UpdateManyBuilder UpdateMany => new();
    public InsertOneBuilder  InsertOne  => new();

    public interface IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> Build();
    }

    public class FilterBuilder {
        Func<TEvent, FilterDefinition<T>>? _filterFunc;

        public Func<TEvent, FilterDefinition<T>> GetFilter => Ensure.NotNull(_filterFunc, "Filter function");

        public void Filter(BuildFilter<TEvent, T> buildFilter)
            => _filterFunc = evt => buildFilter(evt, Builders<T>.Filter);

        public void Filter(Func<TEvent, T, bool> filter)
            => _filterFunc = evt => new ExpressionFilterDefinition<T>(x => filter(evt, x));

        public void Id(GetDocumentId<TEvent> getId) => Filter((evt, filter) => filter.Eq(x => x.Id, getId(evt)));
    }

    public class UpdateOneBuilder : UpdateBuilder<UpdateOneBuilder>, IMongoProjectorBuilder {
        public UpdateOneBuilder Id(GetDocumentId<TEvent> getId) {
            _filter.Id(getId);
            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => collection
                    .UpdateOneAsync(
                        _filter.GetFilter(ctx.Message),
                        GetUpdate(ctx.Message, Builders<T>.Update).Set(x => x.Position, ctx.StreamPosition),
                        cancellationToken: token
                    )
            );
    }

    public class UpdateManyBuilder : UpdateBuilder<UpdateManyBuilder>, IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => collection.UpdateManyAsync(
                    _filter.GetFilter(ctx.Message),
                    GetUpdate(ctx.Message, Builders<T>.Update).Set(x => x.Position, ctx.StreamPosition),
                    cancellationToken: token
                )
            );
    }

    public abstract class UpdateBuilder<TBuilder> where TBuilder : UpdateBuilder<TBuilder> {
        protected readonly FilterBuilder _filter = new();

        BuildUpdate<TEvent, T>? _buildUpdate;

        protected BuildUpdate<TEvent, T> GetUpdate => Ensure.NotNull(_buildUpdate, "Update function");

        public TBuilder Filter(BuildFilter<TEvent, T> buildFilter) {
            _filter.Filter(buildFilter);
            return (TBuilder)this;
        }

        public TBuilder Filter(Func<TEvent, T, bool> filter) {
            _filter.Filter(filter);
            return (TBuilder)this;
        }

        public TBuilder Update(BuildUpdate<TEvent, T> buildUpdate) {
            _buildUpdate = buildUpdate;
            return (TBuilder)this;
        }
    }

    public class InsertOneBuilder : IMongoProjectorBuilder {
        Func<MessageConsumeContext<TEvent>, T>? _getDocument;

        Func<MessageConsumeContext<TEvent>, T> GetDocument => Ensure.NotNull(_getDocument, "Get document function");

        public InsertOneBuilder Document(Func<TEvent, T> getDocument) {
            _getDocument = ctx => getDocument(ctx.Message) with { Position = ctx.StreamPosition };
            return this;
        }

        public InsertOneBuilder Document(Func<MessageConsumeContext<TEvent>, T> getDocument) {
            _getDocument = getDocument;
            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var doc = GetDocument(ctx);
                    return collection.InsertOneAsync(doc, token);
                }
            );
    }

    static ProjectTypedEvent<T, TEvent> GetHandler(
        Func<MessageConsumeContext<TEvent>, IMongoCollection<T>, CancellationToken, Task> handler
    ) {
        return Handle;

        ValueTask<Operation<T>> Handle(MessageConsumeContext<TEvent> ctx)
            => new(new CollectionOperation<T>((collection, token) => handler(ctx, collection, token)));
    }
}
