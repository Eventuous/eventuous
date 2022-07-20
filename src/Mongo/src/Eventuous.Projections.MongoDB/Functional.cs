// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;

namespace Eventuous.Projections.MongoDB;

public delegate string GetDocumentIdFromEvent<in TEvent>(TEvent evt);

public delegate string GetDocumentIdFromStream(StreamName evt);

public delegate string GetDocumentIdFromContext<in TEvent>(IMessageConsumeContext<TEvent> evt) where TEvent : class;

public delegate UpdateDefinition<T> BuildUpdate<T>(UpdateDefinitionBuilder<T> update);

public delegate UpdateDefinition<T> BuildUpdate<in TEvent, T>(IMessageConsumeContext<TEvent> ctx, UpdateDefinitionBuilder<T> update) where TEvent : class;

public delegate UpdateDefinition<T> BuildUpdateFromEvent<in TEvent, T>(TEvent ctx, UpdateDefinitionBuilder<T> update) where TEvent : class;

public delegate ValueTask<UpdateDefinition<T>> BuildUpdateFromEventAsync<in TEvent, T>(TEvent evt, UpdateDefinitionBuilder<T> update) where TEvent : class;

public delegate ValueTask<UpdateDefinition<T>> BuildUpdateAsync<in TEvent, T>(IMessageConsumeContext<TEvent> evt, UpdateDefinitionBuilder<T> update) where TEvent : class;

public delegate FilterDefinition<T> BuildFilter<T>(FilterDefinitionBuilder<T> filter);

public delegate FilterDefinition<T> BuildFilter<in TEvent, T>(IMessageConsumeContext<TEvent> ctx, FilterDefinitionBuilder<T> filter) where TEvent : class;

public delegate IndexKeysDefinition<T> BuildIndex<T>(IndexKeysDefinitionBuilder<T> builder);
    
public delegate UpdateDefinition<T> BuildBulkUpdate<T>(T document, UpdateDefinitionBuilder<T> update);

public delegate FilterDefinition<T> BuildBulkFilter<T>(T document, FilterDefinitionBuilder<T> filter);