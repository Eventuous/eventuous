// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

using Tools;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class UpdateOneBuilder : UpdateBuilder<UpdateOneBuilder>, IMongoProjectorBuilder, IMongoBulkBuilderFactory {
        [PublicAPI]
        public UpdateOneBuilder IdFromStream(GetDocumentIdFromStream getId)
            => Id(x => getId(x.Stream));

        public UpdateOneBuilder Id(GetDocumentIdFromContext<TEvent> getId) {
            FilterBuilder.Id(getId);

            return this;
        }

        public UpdateOneBuilder DefaultId()
            => IdFromStream(streamName => streamName.GetId());

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                async (ctx, collection, token) => {
                    var (update, options) = await GetUpdateWithOptions(ctx);
                    var filter = FilterBuilder.GetFilter(ctx);
                    // TODO: Make this an option (idempotence based on commit position)
                    // var filter = Builders<T>.Filter.And(
                    //     Builders<T>.Filter.Lt(x => x.Position, ctx.GlobalPosition),
                    //     FilterBuilder.GetFilter(ctx)
                    // );

                    await collection
                        .UpdateOneAsync(
                            filter,
                            update,
                            options,
                            token
                        );
                }
            );
        
        BuildWriteModel IMongoBulkBuilderFactory.GetBuilder() => async ctx => {
            var (update, options) = await GetUpdateWithOptions(ctx);
            return new UpdateOneModel<T>(FilterBuilder.GetFilter(ctx), update) {
                Collation    = options.Collation,
                Hint         = options.Hint,
                IsUpsert     = options.IsUpsert,
                ArrayFilters = options.ArrayFilters
            };
        };
    }

    public class UpdateManyBuilder : UpdateBuilder<UpdateManyBuilder>, IMongoProjectorBuilder, IMongoBulkBuilderFactory {
        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                async (ctx, collection, token) => {
                    var (update, options) = await GetUpdateWithOptions(ctx);
                    await collection.UpdateManyAsync(
                            FilterBuilder.GetFilter(ctx),
                            update,
                            options,
                            token
                        )
                        .NoContext();
                }
            );

        BuildWriteModel IMongoBulkBuilderFactory.GetBuilder() => async ctx => {
            var (update, options) = await GetUpdateWithOptions(ctx);
            return new UpdateManyModel<T>(FilterBuilder.GetFilter(ctx), update) {
                Collation = options.Collation,
                Hint = options.Hint,
                IsUpsert = options.IsUpsert,
                ArrayFilters = options.ArrayFilters
            };
        };
    }

    public abstract class UpdateBuilder<TBuilder> where TBuilder : UpdateBuilder<TBuilder> {
        protected readonly FilterBuilder FilterBuilder = new();
        Action<UpdateOptions>?           _configureOptions;

        BuildUpdateAsync<TEvent, T>? _buildUpdate;

        BuildUpdateAsync<TEvent, T> GetUpdate => Ensure.NotNull(_buildUpdate, "Update function");

        static UpdateOptions DefaultOptions => new() { IsUpsert = true };

        public TBuilder Filter(BuildFilter<TEvent, T> buildFilter) {
            FilterBuilder.Filter(buildFilter);

            return Self;
        }

        [PublicAPI]
        public TBuilder Filter(Func<IMessageConsumeContext<TEvent>, T, bool> filter) {
            FilterBuilder.Filter(filter);

            return Self;
        }

        public TBuilder UpdateFromContext(BuildUpdateAsync<TEvent, T> buildUpdate) {
            _buildUpdate = buildUpdate;

            return Self;
        }

        [PublicAPI]
        public TBuilder Update(BuildUpdateFromEventAsync<TEvent, T> buildUpdate) {
            _buildUpdate = (ctx, update) => buildUpdate(ctx.Message, update);

            return Self;
        }

        public TBuilder UpdateFromContext(BuildUpdate<TEvent, T> buildUpdate) {
            _buildUpdate = (ctx, update) => new ValueTask<UpdateDefinition<T>>(buildUpdate(ctx, update));

            return Self;
        }

        public TBuilder Update(BuildUpdateFromEvent<TEvent, T> buildUpdate) {
            _buildUpdate = (ctx, update) => new ValueTask<UpdateDefinition<T>>(buildUpdate(ctx.Message, update));

            return Self;
        }

        [PublicAPI]
        public TBuilder Configure(Action<UpdateOptions> configure) {
            _configureOptions = configure;

            return Self;
        }

        TBuilder Self => (TBuilder)this;
        

        protected async Task<(UpdateDefinition<T> Update, UpdateOptions Options)> GetUpdateWithOptions(IMessageConsumeContext<TEvent> ctx) {
            var options = Options<UpdateOptions>.DefaultIfNotConfigured(_configureOptions, () => DefaultOptions);
            var update  = await GetUpdate(ctx, Builders<T>.Update).NoContext();

            return (
                update
                    .Set(x => x.StreamPosition, ctx.StreamPosition)
                    .Set(x => x.Position, ctx.GlobalPosition), 
                options);
        }
    }
}
