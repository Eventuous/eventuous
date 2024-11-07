// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Polly;

namespace Eventuous.ElasticSearch.Index;

public static class SetupIndex {
    // ReSharper disable once StaticMemberInGenericType
    static readonly AsyncPolicy RetryPolicy = Polly.Policy
        .Handle<ElasticsearchClientException>()
        .WaitAndRetryAsync(10, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            (_, _, _) => {
                // Log.Warning("Elasticsearch exception encountered. Retrying in {TimeSpan}", span);
            });
    
    public static async Task CreateIndexIfNecessary<T>(this IElasticClient client, IndexConfig config) where T : class {
        var lifecycleConfig = Ensure.NotNull(config.Lifecycle, nameof(config.Lifecycle));
        var templateConfig  = Ensure.NotNull(config.Template, nameof(config.Template));

        await RetryPolicy.ExecuteAsync(CreateLifecycle);
        await RetryPolicy.ExecuteAsync(CreateTemplate);

        async Task CreateLifecycle() {
            // Log.Information("Checking if lifecycle {LifecycleName} exists", lifecycleConfig.PolicyName);
            var getLifecycleResponse =
                await client.IndexLifecycleManagement.GetLifecycleAsync(
                    x => x.PolicyId(Ensure.NotEmptyString(lifecycleConfig.PolicyName))
                );

            if (getLifecycleResponse.Policies.ContainsKey(lifecycleConfig.PolicyName)) {
                // Log.Information("Lifecycle {LifecycleName} exists", lifecycleConfig.PolicyName);
                return;
            }

            // Log.Information("Creating lifecycle {LifecycleName}", lifecycleConfig.PolicyName);
            var lifecycleResponse = await client.IndexLifecycleManagement.PutLifecycleAsync(
                lifecycleConfig.PolicyName,
                p => p.Policy(
                    pd => pd.Phases(phases => lifecycleConfig.Tiers?.Aggregate(phases, AddTier))
                )
            );

            if (!lifecycleResponse.IsValid) {
                throw getLifecycleResponse.OriginalException ??
                    new ApplicationException(
                        $"Unable to create lifecycle policy: {lifecycleResponse.DebugInformation}"
                    );
            }
        }

        async Task CreateTemplate() {
            // Log.Information("Checking if template {TemplateName} exists", templateConfig.TemplateName);
            var templateExists =
                await client.LowLevel.Indices.TemplateV2ExistsForAllAsync<IndexTemplateV2ExistsResponse>(
                    Ensure.NotEmptyString(templateConfig.TemplateName)
                );

            if (templateExists.Exists) {
                // Log.Information("Template {TemplateName} exists", templateConfig.TemplateName);
                return;
            }

            // Log.Information("Creating template {TemplateName}", templateConfig.TemplateName);
            var templateResponse = await client.LowLevel.Indices.PutTemplateV2ForAllAsync<StringResponse>(
                templateConfig.TemplateName,
                PostData.Serializable(
                    new {
                        index_patterns = new[] { config.IndexName },
                        data_stream    = new { },
                        template = new {
                            settings = new {
                                index = new {
                                    lifecycle          = new { name = lifecycleConfig.PolicyName },
                                    number_of_shards   = templateConfig.NumberOfShards,
                                    number_of_replicas = templateConfig.NumberOrReplicas
                                }
                            },
                            mappings = new TypeMappingDescriptor<T>().AutoMap()
                        }
                    }
                )
            );

            if (!templateResponse.Success) {
                throw templateResponse.OriginalException ??
                    new ApplicationException(
                        $"Unable to create template: {templateResponse.DebugInformation}"
                    );
            }
        }
    }

    // ReSharper disable once CognitiveComplexity
    static PhasesDescriptor AddTier(PhasesDescriptor descriptor, TierDefinition definition) {
        return definition.Tier switch {
            "hot"    => descriptor.Hot(GetPhase),
            "warm"   => descriptor.Warm(GetPhase),
            "cold"   => descriptor.Cold(GetPhase),
            "frozen" => descriptor.Frozen(GetPhase),
            "delete" => descriptor.Delete(GetPhase),
            _ => throw new ArgumentOutOfRangeException(
                nameof(definition),
                $"Unknown tier: '{definition.Tier}'"
            )
        };

        IPhase GetPhase(PhaseDescriptor desc) {
            if (definition.MinAge is { } minAge) desc.MinimumAge(minAge);

            return desc.Actions(
                lifecycle => {
                    if (definition.Rollover is var (maxAge, maxDocs, maxSize)) {
                        lifecycle.Rollover(
                            x => {
                                if (maxAge is not null) x.MaximumAge(maxAge);
                                if (maxDocs is not null) x.MaximumDocuments(maxDocs);
                                if (maxSize is not null) x.MaximumSize(maxSize);
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
