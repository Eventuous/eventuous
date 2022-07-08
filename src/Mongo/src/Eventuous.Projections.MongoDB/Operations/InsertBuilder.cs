// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public class InsertOneBuilder : IMongoProjectorBuilder {
        Func<MessageConsumeContext<TEvent>, T>? _getDocument;
        Action<InsertOneOptions>?               _configureOptions;

        Func<MessageConsumeContext<TEvent>, T> GetDocument => Ensure.NotNull(_getDocument, "Get document function");

        public InsertOneBuilder Document(Func<StreamName, TEvent, T> getDocument) {
            _getDocument = ctx => getDocument(ctx.Stream, ctx.Message) with { Position = ctx.StreamPosition };
            return this;
        }

        public InsertOneBuilder Document(Func<MessageConsumeContext<TEvent>, T> getDocument) {
            _getDocument = getDocument;
            return this;
        }

        public InsertOneBuilder Configure(Action<InsertOneOptions> configure) {
            _configureOptions = configure;
            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                handler: (ctx, collection, token) => {
                    var options = new InsertOneOptions();
                    _configureOptions?.Invoke(options);
                    var doc = GetDocument(ctx);
                    return collection.InsertOneAsync(doc, options, token);
                }
            );
    }

    public class InsertManyBuilder : IMongoProjectorBuilder {
        Func<MessageConsumeContext<TEvent>, IEnumerable<T>>? _getDocuments;
        Action<InsertManyOptions>?                           _configureOptions;

        Func<MessageConsumeContext<TEvent>, IEnumerable<T>> GetDocuments
            => Ensure.NotNull(_getDocuments, "Get documents function");

        public InsertManyBuilder Documents(Func<TEvent, IEnumerable<T>> getDocuments) {
            _getDocuments = ctx => getDocuments(ctx.Message).Select(x => x with { Position = ctx.StreamPosition });
            return this;
        }

        public InsertManyBuilder Documents(Func<MessageConsumeContext<TEvent>, IEnumerable<T>> getDocuments) {
            _getDocuments = getDocuments;
            return this;
        }

        public InsertManyBuilder Configure(Action<InsertManyOptions> configure) {
            _configureOptions = configure;
            return this;
        }

        ProjectTypedEvent<T, TEvent> IMongoProjectorBuilder.Build()
            => GetHandler(
                (ctx, collection, token) => {
                    var options = new InsertManyOptions();
                    _configureOptions?.Invoke(options);
                    var docs = GetDocuments(ctx);
                    return collection.InsertManyAsync(docs, options, token);
                }
            );
    }
}
