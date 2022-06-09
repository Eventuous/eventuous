// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Projections.MongoDB.Tools;
using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

public partial class MongoOperationBuilder<TEvent, T> where T : ProjectedDocument where TEvent : class {
    public UpdateOneBuilder  UpdateOne  => new();
    public UpdateManyBuilder UpdateMany => new();
    public InsertOneBuilder  InsertOne  => new();
    public InsertManyBuilder InsertMany => new();
    public DeleteOneBuilder  DeleteOne  => new();
    public DeleteManyBuilder DeleteMany => new();

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

    static ProjectTypedEvent<T, TEvent> GetHandler(
        Func<MessageConsumeContext<TEvent>, IMongoCollection<T>, CancellationToken, Task> handler
    ) {
        return Handle;

        ValueTask<MongoProjectOperation<T>> Handle(MessageConsumeContext<TEvent> ctx)
            => new(new MongoProjectOperation<T>((collection, token) => handler(ctx, collection, token)));
    }
}
