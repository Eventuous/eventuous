// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddEventuousPostgres(
        this IServiceCollection services,
        string                  connectionString,
        PostgresStoreOptions?   storeOptions = null
    ) {
        var options = storeOptions ?? new PostgresStoreOptions();

        return services.AddNpgsqlDataSource(
            connectionString,
            builder => builder.MapComposite<NewPersistedEvent>(new Schema(options.Schema).StreamMessage)
        );
    }
}
