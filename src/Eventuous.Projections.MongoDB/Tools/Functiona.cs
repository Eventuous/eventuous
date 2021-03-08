using MongoDB.Driver;

namespace Eventuous.Projections.MongoDB.Tools {
    public delegate UpdateDefinition<T> BuildUpdate<T>(UpdateDefinitionBuilder<T> update);

    public delegate FilterDefinition<T> BuildFilter<T>(FilterDefinitionBuilder<T> filter);

    public delegate IndexKeysDefinition<T> BuildIndex<T>(IndexKeysDefinitionBuilder<T> builder);
    
    public delegate UpdateDefinition<T> BuildBulkUpdate<T>(T document, UpdateDefinitionBuilder<T> update);

    public delegate FilterDefinition<T> BuildBulkFilter<T>(T document, FilterDefinitionBuilder<T> filter);

}