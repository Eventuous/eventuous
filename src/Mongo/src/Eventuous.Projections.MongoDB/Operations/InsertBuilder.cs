// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

using Tools;

public partial class MongoOperationBuilder<TEvent, T>  where T : ProjectedDocument where TEvent : class {
    public class InsertOneBuilder : IMongoProjectorBuilder, IMongoBulkBuilderFactory {
        Func<MessageConsumeContext<TEvent>, T>? _getDocument;
        Action<InsertOneOptions>?               _configureOptions;

        Func<MessageConsumeContext<TEvent>, T> GetDocument => Ensure.NotNull(_getDocument, "Get document function");

        public InsertOneBuilder Document(Func<StreamName, TEvent, T> getDocument) 
            => Document(ctx => getDocument(ctx.Stream, ctx.Message));

        public InsertOneBuilder Document(Func<MessageConsumeContext<TEvent>, T> getDocument) {
            _getDocument = ctx => getDocument(ctx) with {
                Position = ctx.GlobalPosition,
                StreamPosition = ctx.StreamPosition
            };

            return this;
        }

        public InsertOneBuilder Configure(Action<InsertOneOptions> configure) {
            _configureOptions = configure;

            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                handler: (ctx, collection, token) => {
                    var options = Options<InsertOneOptions>.NullIfNotConfigured(_configureOptions);
                    var doc     = GetDocument(ctx);

                    return collection.InsertOneAsync(doc, options, token);
                }
            );

        BuildWriteModel IMongoBulkBuilderFactory.GetBuilder() => ctx
            => new ValueTask<WriteModel<T>>(new InsertOneModel<T>(GetDocument(ctx)));
    }

    public class InsertManyBuilder : IMongoProjectorBuilder {
        Func<MessageConsumeContext<TEvent>, IEnumerable<T>>? _getDocuments;
        Action<InsertManyOptions>?                           _configureOptions;

        Func<MessageConsumeContext<TEvent>, IEnumerable<T>> GetDocuments => Ensure.NotNull(_getDocuments, "Get documents function");

        public InsertManyBuilder Documents(Func<TEvent, IEnumerable<T>> getDocuments) 
            => Documents(ctx => getDocuments(ctx.Message));

        public InsertManyBuilder Documents(Func<MessageConsumeContext<TEvent>, IEnumerable<T>> getDocuments) {
            _getDocuments = ctx => getDocuments(ctx)
                .Select(x => x with 
                {
                    Position = ctx.GlobalPosition,
                    StreamPosition = ctx.StreamPosition
                });

            return this;
        }

        public InsertManyBuilder Configure(Action<InsertManyOptions> configure) {
            _configureOptions = configure;

            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = Options<InsertManyOptions>.NullIfNotConfigured(_configureOptions);
                    var docs = GetDocuments(ctx);
                    
                    return collection.InsertManyAsync(docs, options, token);
                }
            );
    }
}
