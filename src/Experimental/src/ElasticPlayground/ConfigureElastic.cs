using Eventuous.ElasticSearch.Index;
using Eventuous.ElasticSearch.Store;
using Nest;

namespace ElasticPlayground;

public static class ConfigureElastic {
    public static async Task ConfigureIndex(this ElasticClient client) {
        var config = new IndexConfig {
            IndexName = "eventuous",
            Lifecycle = new LifecycleConfig {
                PolicyName = "eventuous",
                Tiers = [
                    new TierDefinition {
                        Tier     = "hot",
                        MinAge   = "1d",
                        Priority = 100,
                        Rollover = new Rollover {
                            MaxAge  = "1d",
                            MaxSize = "100mb"
                        }
                    },
                    new TierDefinition {
                        Tier       = "warm",
                        MinAge     = "1d",
                        Priority   = 50,
                        ForceMerge = new ForceMerge { MaxNumSegments = 1 }
                    },
                    new TierDefinition {
                        Tier     = "cold",
                        MinAge   = "1d",
                        Priority = 0,
                        ReadOnly = true
                    }
                ]
            },
            Template = new DataStreamTemplateConfig {
                TemplateName = "eventuous"
            }
        };

        await client.CreateIndexIfNecessary<PersistedEvent>(config);
    }
}
