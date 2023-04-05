// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Postgresql.Extensions;

public static class ServiceCollectionExtensions {
    public static IServiceCollection ConfigureEventuousPostgres(this IServiceCollection services, string connectionString)
        => ConfigureEventuousPostgres(services, connectionString, new PostgresStoreOptions());

    public static IServiceCollection ConfigureEventuousPostgres(
        this IServiceCollection services,
        string                  connectionString,
        PostgresStoreOptions    storeOptions
    ) =>
        services.AddNpgsqlDataSource(connectionString,
            options =>
                options.MapComposite<NewPersistedEvent>(new Schema(storeOptions.Schema).StreamMessage));
}