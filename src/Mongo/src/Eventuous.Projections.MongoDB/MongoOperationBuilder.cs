// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

using Tools;

public partial class MongoOperationBuilder<TEvent, T>
    where T : ProjectedDocument where TEvent : class {
    public UpdateOneBuilder  UpdateOne  => new();
    public UpdateManyBuilder UpdateMany => new();
    public InsertOneBuilder  InsertOne  => new();
    public InsertManyBuilder InsertMany => new();
    public DeleteOneBuilder  DeleteOne  => new();
    public DeleteManyBuilder DeleteMany => new();
    public BulkWriteBuilder  Bulk       => new();

    public class MongoBulkOperationBuilders {
        MongoBulkOperationBuilders() { }
        internal static MongoBulkOperationBuilders Instance { get; } = new();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public UpdateOneBuilder UpdateOne => new();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public UpdateManyBuilder UpdateMany => new();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public InsertOneBuilder InsertOne => new();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public DeleteOneBuilder DeleteOne => new();
        // ReSharper disable once MemberCanBeMadeStatic.Global
        public DeleteManyBuilder DeleteMany => new();
    }

    public interface IMongoProjectorBuilder {
        ProjectTypedEvent<T, TEvent> Build();
    }

    public interface IMongoBulkBuilderFactory {
        BuildWriteModel<T, TEvent> GetBuilder();
    }

    public class FilterBuilder {
        Func<IMessageConsumeContext<TEvent>, FilterDefinition<T>>? _filterFunc;

        public Func<IMessageConsumeContext<TEvent>, FilterDefinition<T>> GetFilter => Ensure.NotNull(_filterFunc, "Filter function");

        public void Filter(BuildFilter<TEvent, T> buildFilter) => _filterFunc = evt => buildFilter(evt, Builders<T>.Filter);

        public void Filter(Func<IMessageConsumeContext<TEvent>, T, bool> filter) => _filterFunc = evt => new ExpressionFilterDefinition<T>(x => filter(evt, x));

        public void Id(GetDocumentIdFromContext<TEvent> getId) => Filter((ctx, filter) => filter.Eq(x => x.Id, getId(ctx)));
    }

    static ProjectTypedEvent<T, TEvent> GetHandler(Func<MessageConsumeContext<TEvent>, IMongoCollection<T>, CancellationToken, Task> handler) {
        return Handle;

        ValueTask<MongoProjectOperation<T>> Handle(MessageConsumeContext<TEvent> ctx)
            => new(new MongoProjectOperation<T>((collection, token) => handler(ctx, collection, token)));
    }
}
