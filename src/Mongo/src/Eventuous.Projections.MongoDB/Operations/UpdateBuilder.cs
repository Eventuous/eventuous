// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;

namespace Eventuous.Projections.MongoDB;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
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
            return Self;
        }

        public TBuilder Filter(Func<TEvent, T, bool> filter) {
            _filter.Filter(filter);
            return Self;
        }

        public TBuilder Update(BuildUpdate<TEvent, T> buildUpdate) {
            _buildUpdate = buildUpdate;
            return Self;
        }

        TBuilder Self => (TBuilder)this;
    }
}
