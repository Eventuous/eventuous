// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;

namespace Eventuous.Projections.MongoDB.Tools; 

[PublicAPI]
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

        ConventionRegistry.Register("EventuousConventions", pack, _ => true);
    }
}