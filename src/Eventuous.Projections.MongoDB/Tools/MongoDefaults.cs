using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Eventuous.Projections.MongoDB.Tools {
    public static class MongoDefaults {
        public static readonly BulkWriteOptions DefaultBulkWriteOptions = new() {IsOrdered = false};
        public static readonly UpdateOptions    DefaultUpdateOptions    = new() {IsUpsert  = true};
        public static readonly ReplaceOptions   DefaultReplaceOptions   = new() {IsUpsert  = true};

        public static void RegisterConventions() {
            if (BsonClassMap.IsClassMapRegistered(typeof(Document))) return;

            var pack = new ConventionPack {
                new CamelCaseElementNameConvention(),
                new IgnoreIfNullConvention(true),
                new IgnoreExtraElementsConvention(true)
            };

            ConventionRegistry.Register("EventuousConventions", pack, type => true);
        }
    }
}
