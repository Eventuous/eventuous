// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

using Tools;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class DeleteOneBuilder : DeleteBuilder<DeleteOneBuilder>, IMongoProjectorBuilder {
        public DeleteOneBuilder Id(GetDocumentIdFromStream getId)
            => Id(x => getId(x.Stream));

        public DeleteOneBuilder Id(GetDocumentIdFromContext<TEvent> getId) {
            FilterBuilder.Id(getId);

            return this;
        }

        public DeleteOneBuilder DefaultId()
            => Id(x => x.GetId());

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = Options<DeleteOptions>.New(ConfigureOptions);
                    return collection.DeleteOneAsync(FilterBuilder.GetFilter(ctx), options, token);
                });
    }

    public class DeleteManyBuilder : DeleteBuilder<DeleteManyBuilder>, IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = Options<DeleteOptions>.New(ConfigureOptions);

                    return collection.DeleteManyAsync(FilterBuilder.GetFilter(ctx), options, token);
                });
    }

    public abstract class DeleteBuilder<TBuilder> : IMongoBulkBuilderFactory where TBuilder : DeleteBuilder<TBuilder> {
        protected readonly FilterBuilder          FilterBuilder = new();
        protected          Action<DeleteOptions>? ConfigureOptions;

        public TBuilder Filter(BuildFilter<TEvent, T> buildFilter) {
            FilterBuilder.Filter(buildFilter);

            return Self;
        }

        public TBuilder Filter(Func<IMessageConsumeContext<TEvent>, T, bool> filter) {
            FilterBuilder.Filter(filter);

            return Self;
        }

        public TBuilder Configure(Action<DeleteOptions> configure) {
            ConfigureOptions = configure;

            return Self;
        }

        TBuilder Self => (TBuilder)this;

        BuildWriteModel IMongoBulkBuilderFactory.GetBuilder() => ctx=> {
                var options = Options<DeleteOptions>.New(ConfigureOptions);
                return new ValueTask<WriteModel<T>>(
                new DeleteOneModel<T>(FilterBuilder.GetFilter(ctx)) {
                    Collation = options.Collation,
                    Hint = options.Hint
                }
            );
        };
    }
}
