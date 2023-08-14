// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    public static IServiceCollection AddEventuousPostgres(
        this IServiceCollection services,
        string                  connectionString,
        PostgresStoreOptions?   storeOptions = null
    ) {
        var options = storeOptions ?? new PostgresStoreOptions();

        services.AddNpgsqlDataSource(
            connectionString,
            builder => builder.MapComposite<NewPersistedEvent>(new Schema(options.Schema).StreamMessage)
        );
        services.AddSingleton<PostgresStore>(
            provider => {
                var dataSource      = provider.GetRequiredService<NpgsqlDataSource>();
                var opt             = provider.GetService<IOptions<PostgresStoreOptions>>()?.Value ?? provider.GetService<PostgresStoreOptions>() ?? options;
                var eventSerializer = provider.GetService<IEventSerializer>();
                var metaSerializer = provider.GetService<IMetadataSerializer>();

                return new PostgresStore(dataSource, opt, eventSerializer, metaSerializer);
            });

        return services;
    }
}
