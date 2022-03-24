using Elasticsearch.Net;
using Nest;
using static Elasticsearch.Net.PostData;

namespace Eventuous.ElasticSearch;

public static class DomainEventsIndexDefinition {
    const string IndexLifecyclePolicyName = "eventlog-policy";

    public static async Task SetupDomainEventsIndex(this IElasticClient client) {
        await client.CreateIndexLifecycleManagement();
        await client.CreateDataStreamTemplate("eventlog-template");
    }

    static Task<PutLifecycleResponse> CreateIndexLifecycleManagement(this IElasticClient client) =>
        client.IndexLifecycleManagement.PutLifecycleAsync(
            IndexLifecyclePolicyName,
            p => p
                .Policy(
                    po => po
                        .Phases(
                            a => a
                                .Hot(
                                    w => w.MinimumAge("10d")
                                        .Actions(
                                            ac => ac
                                                .Rollover(
                                                    f => f
                                                        .MaximumSize("50gb")
                                                        .MaximumAge("100d")
                                                )
                                                .SetPriority(f => f.Priority(100))
                                        )
                                )
                                .Warm(
                                    w => w.MinimumAge("10d")
                                        .Actions(
                                            ac => ac
                                                .ForceMerge(f => f.MaximumNumberOfSegments(1))
                                                .SetPriority(f => f.Priority(50))
                                        )
                                )
                                .Cold(
                                    w => w.MinimumAge("10d")
                                        .Actions(
                                            ac => ac
                                                .SetPriority(f => f.Priority(0))
                                                .ReadOnly(_ => new ReadOnlyLifecycleAction())
                                        )
                                )
                        )
                )
        );

    static Task<StringResponse> CreateDataStreamTemplate(this IElasticClient client, string templateName) =>
        client.LowLevel.Indices.PutTemplateV2ForAllAsync<StringResponse>(
            $"{templateName}_data_stream",
            Serializable(
                new {
                    index_patterns = new[] { IndexNames.DomainEventsIndex },
                    data_stream    = new { },
                    template = new {
                        settings = new {
                            index = new {
                                lifecycle          = new { name = IndexLifecyclePolicyName },
                                number_of_shards   = 1,
                                number_of_replicas = 1
                            }
                        },
                        mappings = new TypeMappingDescriptor<PersistedEvent>().AutoMap()
                    }
                }
            )
        );
}