// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection1;

public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds PostgreSQL event store and the necessary schema to the DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Connection string</param>
    /// <param name="schema">Schema name</param>
    /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
    /// <returns></returns>
    public static IServiceCollection AddEventuousSqlServer(
            this IServiceCollection services,
            string                  connectionString,
            string                  schema             = Schema.DefaultSchema,
            bool                    initializeDatabase = false
        ) {
        var options = new SqlServerStoreOptions {
            Schema             = schema,
            ConnectionString   = connectionString,
            InitializeDatabase = initializeDatabase
        };
        services.AddSingleton(options);
        services.AddSingleton<SqlServerStore>();
        services.AddHostedService<SchemaInitializer>();

        return services;
    }

    public static IServiceCollection AddEventuousSqlServer(this IServiceCollection services, IConfiguration config) {
        services.Configure<SqlServerStoreOptions>(config);
        services.AddSingleton<SqlServerStoreOptions>(sp => sp.GetRequiredService<IOptions<SqlServerStoreOptions>>().Value);
        services.AddSingleton<SqlServerStore>();
        services.AddHostedService<SchemaInitializer>();

        return services;
    }
}
