// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Projections.MongoDB;

using Tools;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class BulkWriteBuilder : IMongoProjectorBuilder {
        Action<BulkWriteOptions>?          _configureOptions;
        readonly List<BuildWriteModel>     _builders = new();

        public BulkWriteBuilder Operation<TFactory>(Func<MongoBulkOperationBuilders, TFactory> getBuilderFactory) 
            where TFactory: IMongoBulkBuilderFactory {
                var factory = getBuilderFactory(new MongoBulkOperationBuilders());
                _builders.Add(factory.GetBuilder());

                return this;
        }
        
        public BulkWriteBuilder Configure(Action<BulkWriteOptions> configure) {
            _configureOptions = configure;

            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build() => 
            GetHandler(async (ctx, collection, token) => {
                var options = Options<BulkWriteOptions>.New(_configureOptions);
                var models = await _builders.Select(build => build(ctx)).WhenAll();
                await collection.BulkWriteAsync(models, options, token);
        });
    }
}