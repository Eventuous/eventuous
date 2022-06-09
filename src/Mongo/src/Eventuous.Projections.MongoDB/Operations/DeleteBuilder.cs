// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;

namespace Eventuous.Projections.MongoDB;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class DeleteOneBuilder : DeleteBuilder<DeleteOneBuilder>, IMongoProjectorBuilder {
        public DeleteOneBuilder Id(GetDocumentId<TEvent> getId) {
            _filter.Id(getId);
            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = new DeleteOptions();
                    _configureOptions?.Invoke(options);
                    return collection.DeleteOneAsync(_filter.GetFilter(ctx.Message), options, token);
                }
            );
    }

    public class DeleteManyBuilder : DeleteBuilder<DeleteManyBuilder>, IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = new DeleteOptions();
                    _configureOptions?.Invoke(options);
                    return collection.DeleteManyAsync(_filter.GetFilter(ctx.Message), options, token);
                }
            );
    }

    public abstract class DeleteBuilder<TBuilder> where TBuilder : DeleteBuilder<TBuilder> {
        protected readonly FilterBuilder          _filter = new();
        protected          Action<DeleteOptions>? _configureOptions;

        public TBuilder Filter(BuildFilter<TEvent, T> buildFilter) {
            _filter.Filter(buildFilter);
            return Self;
        }

        public TBuilder Filter(Func<TEvent, T, bool> filter) {
            _filter.Filter(filter);
            return Self;
        }

        public TBuilder Configure(Action<DeleteOptions> configure) {
            _configureOptions = configure;
            return Self;
        }

        TBuilder Self => (TBuilder)this;
    }
}
