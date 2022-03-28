using Elasticsearch.Net;
using Nest;

namespace Eventuous.Connectors.EsdbElastic.Index;

public static class SetupIndex {
    public static async Task CreateIfNecessary(IElasticClient client, IndexConfig config) {
        var (templateConfig, (policyName, tierDefinitions)) = config;

        var response = await client.IndexLifecycleManagement.GetLifecycleAsync(x => x.PolicyId(policyName));
        if (response.IsValid && response.Policies.ContainsKey(policyName)) return;

        await client.IndexLifecycleManagement.PutLifecycleAsync(
            policyName,
            p => p.Policy(
                pd => pd.Phases(phases => tierDefinitions.Aggregate(phases, AddTier))
            )
        );

        await client.LowLevel.Indices.PutTemplateV2ForAllAsync<StringResponse>(
            templateConfig.TemplateName,
            PostData.Serializable(
                new {
                    index_patterns = new[] { templateConfig.IndexPattern },
                    data_stream    = new { },
                    template = new {
                        settings = new {
                            index = new {
                                lifecycle          = new { name = policyName },
                                number_of_shards   = templateConfig.NumberOfShards,
                                number_of_replicas = templateConfig.NumberOrReplicas
                            }
                        },
                        mappings = new TypeMappingDescriptor<PersistedEvent>().AutoMap()
                    }
                }
            )
        );
    }

    static PhasesDescriptor AddTier(PhasesDescriptor descriptor, TierDefinition definition) {
        return definition.Tier switch {
            "hot"    => descriptor.Hot(GetPhase),
            "warm"   => descriptor.Warm(GetPhase),
            "cold"   => descriptor.Cold(GetPhase),
            "frozen" => descriptor.Frozen(GetPhase),
            "delete" => descriptor.Delete(GetPhase),
            _        => throw new ArgumentOutOfRangeException(nameof(definition), $"Unknown tier: '{definition.Tier}'")
        };

        IPhase GetPhase(PhaseDescriptor desc) {
            if (definition.MinAge is { } minAge) desc.MinimumAge(minAge);

            return desc.Actions(
                lifecycle => {
                    if (definition.Rollover is var (maxAge, maxDocs, maxSize)) {
                        lifecycle.Rollover(
                            x => {
                                if (maxAge is { }) x.MaximumAge(maxAge);
                                if (maxDocs is { }) x.MaximumDocuments(maxDocs);
                                if (maxSize is { }) x.MaximumSize(maxSize);
                                return x;
                            }
                        );
                    }

                    if (definition.Delete) lifecycle.Delete(_ => new DeleteLifecycleAction());
                    if (definition.ReadOnly) lifecycle.ReadOnly(_ => new ReadOnlyLifecycleAction());
                    if (definition.Priority is { } priority) lifecycle.SetPriority(p => p.Priority(priority));

                    if (definition.ForceMerge is { } forceMerge)
                        lifecycle.ForceMerge(p => p.MaximumNumberOfSegments(forceMerge.MaxNumSegments));

                    return lifecycle;
                }
            );
        }
    }
}