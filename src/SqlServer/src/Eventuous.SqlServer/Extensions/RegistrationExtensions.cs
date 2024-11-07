// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds SQL Server event store and the necessary schema to the DI container.
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
            Schema             = Ensure.NotEmptyString(schema),
            ConnectionString   = Ensure.NotEmptyString(connectionString),
            InitializeDatabase = initializeDatabase
        };
        services.AddSingleton(options);
        services.AddSingleton<SqlServerStore>();
        services.AddHostedService<SchemaInitializer>();
        services.TryAddSingleton(new SqlServerConnectionOptions(connectionString, schema));

        return services;
    }

    /// <summary>
    /// Adds SQL Server event store and the necessary schema to the DI container using the configuration.
    /// </summary>
    /// <param name="services">Services collection</param>
    /// <param name="config">Configuration section for SQL Server options</param>
    /// <returns></returns>
    public static IServiceCollection AddEventuousSqlServer(this IServiceCollection services, IConfiguration config) {
        services.Configure<SqlServerStoreOptions>(config);
        services.AddSingleton<SqlServerStoreOptions>(sp => sp.GetRequiredService<IOptions<SqlServerStoreOptions>>().Value);
        services.AddSingleton<SqlServerStore>();
        services.AddHostedService<SchemaInitializer>();

        services.TryAddSingleton(
            sp => {
                var storeOptions = sp.GetRequiredService<IOptions<SqlServerStoreOptions>>().Value;

                return new SqlServerConnectionOptions(Ensure.NotEmptyString(storeOptions.ConnectionString), storeOptions.Schema);
            }
        );

        return services;
    }

    /// <summary>
    /// Registers the SQL Server-based checkpoint store using the details provided when registering
    /// SQL Server connection factory.
    /// </summary>
    /// <param name="services">Services collection</param>
    /// <returns></returns>
    public static IServiceCollection AddSqlServerCheckpointStore(this IServiceCollection services)
        => services.AddCheckpointStore<SqlServerCheckpointStore>(
            sp => {
                var loggerFactory          = sp.GetService<ILoggerFactory>();
                var connectionOptions      = sp.GetService<SqlServerConnectionOptions>();
                var checkpointStoreOptions = sp.GetService<SqlServerCheckpointStoreOptions>();

                var schema = connectionOptions?.Schema is not null and not Schema.DefaultSchema
                 && checkpointStoreOptions?.Schema is null or Schema.DefaultSchema
                        ? connectionOptions.Schema
                        : checkpointStoreOptions?.Schema ?? Schema.DefaultSchema;
                var connectionString = checkpointStoreOptions?.ConnectionString ?? connectionOptions?.ConnectionString;

                return new(Ensure.NotNull(connectionString), schema, loggerFactory);
            }
        );
}
