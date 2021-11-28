namespace Eventuous.Projections.MongoDB;

public delegate string GetDocumentId<in TEvent>(TEvent evt);

public delegate UpdateDefinition<T> BuildUpdate<T>(UpdateDefinitionBuilder<T> update);

public delegate UpdateDefinition<T> BuildUpdate<in TEvent, T>(TEvent evt, UpdateDefinitionBuilder<T> update);

public delegate Task<UpdateDefinition<T>> BuildUpdateAsync<in TEvent, T>(TEvent evt, UpdateDefinitionBuilder<T> update);

public delegate FilterDefinition<T> BuildFilter<T>(FilterDefinitionBuilder<T> filter);

public delegate FilterDefinition<T> BuildFilter<in TEvent, T>(TEvent evt, FilterDefinitionBuilder<T> filter);

public delegate IndexKeysDefinition<T> BuildIndex<T>(IndexKeysDefinitionBuilder<T> builder);
    
public delegate UpdateDefinition<T> BuildBulkUpdate<T>(T document, UpdateDefinitionBuilder<T> update);

public delegate FilterDefinition<T> BuildBulkFilter<T>(T document, FilterDefinitionBuilder<T> filter);