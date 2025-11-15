using Eventuous.ElasticSearch.Index;
using Eventuous.ElasticSearch.Store;
using Nest;

namespace ElasticPlayground;

public static class ConfigureElastic {
    public static async Task ConfigureIndex(this ElasticClient client) {
        var config = new IndexConfig {
            IndexName = "eventuous",
            Lifecycle = new() {
                PolicyName = "eventuous",
                Tiers = [
                    new() {
                        Tier     = "hot",
                        MinAge   = "1d",
                        Priority = 100,
                        Rollover = new() {
                            MaxAge  = "1d",
                            MaxSize = "100mb"
                        }
                    },
                    new() {
                        Tier       = "warm",
                        MinAge     = "1d",
                        Priority   = 50,
                        ForceMerge = new() { MaxNumSegments = 1 }
                    },
                    new() {
                        Tier     = "cold",
                        MinAge   = "1d",
                        Priority = 0,
                        ReadOnly = true
                    }
                ]
            },
            Template = new() { TemplateName = "eventuous" }
        };

        await client.CreateIndexIfNecessary<PersistedEvent>(config);
    }
}
