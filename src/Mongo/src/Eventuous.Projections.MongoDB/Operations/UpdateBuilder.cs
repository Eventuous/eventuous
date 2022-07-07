// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Tools;

namespace Eventuous.Projections.MongoDB;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class UpdateOneBuilder : UpdateBuilder<UpdateOneBuilder>, IMongoProjectorBuilder {
        public UpdateOneBuilder Id(GetDocumentId getId) {
            _filter.Id(getId);
            return this;
        }

        public UpdateOneBuilder DefaultId() => Id(ctx => ctx.GetId());

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                async (ctx, collection, token) => {
                    var options = new UpdateOptions { IsUpsert = true };
                    _configureOptions?.Invoke(options);
                    var update = await GetUpdate(ctx.Message, Builders<T>.Update);

                    await collection
                        .UpdateOneAsync(
                            _filter.GetFilter(ctx),
                            update.Set(x => x.Position, ctx.StreamPosition),
                            options,
                            token
                        );
                }
            );
    }

    public class UpdateManyBuilder : UpdateBuilder<UpdateManyBuilder>, IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                async (ctx, collection, token) => {
                    var options = new UpdateOptions { IsUpsert = true };
                    _configureOptions?.Invoke(options);
                    var update = await GetUpdate(ctx.Message, Builders<T>.Update).NoContext();

                    await collection.UpdateManyAsync(
                        _filter.GetFilter(ctx),
                        update.Set(x => x.Position, ctx.StreamPosition),
                        options,
                        token
                    ).NoContext();
                }
            );
    }

    public abstract class UpdateBuilder<TBuilder> where TBuilder : UpdateBuilder<TBuilder> {
        protected readonly FilterBuilder          _filter = new();
        protected          Action<UpdateOptions>? _configureOptions;

        BuildUpdateAsync<TEvent, T>? _buildUpdate;

        protected BuildUpdateAsync<TEvent, T> GetUpdate => Ensure.NotNull(_buildUpdate, "Update function");

        public TBuilder Filter(BuildFilter<TEvent, T> buildFilter) {
            _filter.Filter(buildFilter);
            return Self;
        }

        public TBuilder Filter(Func<IMessageConsumeContext<TEvent>, T, bool> filter) {
            _filter.Filter(filter);
            return Self;
        }

        public TBuilder Update(BuildUpdateAsync<TEvent, T> buildUpdate) {
            _buildUpdate = buildUpdate;
            return Self;
        }

        public TBuilder Update(BuildUpdate<TEvent, T> buildUpdate) {
            _buildUpdate = (evt, update) => new ValueTask<UpdateDefinition<T>>(buildUpdate(evt, update));
            return Self;
        }

        public TBuilder Configure(Action<UpdateOptions> configure) {
            _configureOptions = configure;
            return Self;
        }

        TBuilder Self => (TBuilder)this;
    }
}
