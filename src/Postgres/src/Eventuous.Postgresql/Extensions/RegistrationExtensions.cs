// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Postgresql;
using Eventuous.Postgresql.Subscriptions;
using Eventuous.Sql.Base;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    /// <summary>
    /// Adds PostgreSQL event store and the necessary schema to the DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="connectionString">Connection string</param>
    /// <param name="schema">Schema name</param>
    /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
    /// <param name="configureBuilder">Optional: function to configure the data source builder</param>
    /// <param name="connectionLifetime">Optional: lifetime of the connection, default is transient</param>
    /// <param name="dataSourceLifetime">Optional> lifetime of the data source, default is singleton</param>
    /// <returns>Services collection</returns>
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static IServiceCollection AddEventuousPostgres(
            this IServiceCollection                            services,
            string                                             connectionString,
            string                                             schema             = Schema.DefaultSchema,
            bool                                               initializeDatabase = false,
            Action<IServiceProvider, NpgsqlDataSourceBuilder>? configureBuilder   = null,
            ServiceLifetime                                    connectionLifetime = ServiceLifetime.Transient,
            ServiceLifetime                                    dataSourceLifetime = ServiceLifetime.Singleton
        ) {
        var options = new PostgresStoreOptions {
            Schema             = schema,
            ConnectionString   = connectionString,
            InitializeDatabase = initializeDatabase
        };

        services.AddNpgsqlDataSourceCore(
            _ => connectionString,
            (sp, builder) => {
                builder.MapComposite<NewPersistedEvent>(Schema.GetStreamMessageTypeName(schema));
                configureBuilder?.Invoke(sp, builder);
            },
            connectionLifetime,
            dataSourceLifetime
        );
        services.AddSingleton(options);
        services.AddSingleton<PostgresStore>();
        services.AddHostedService<SchemaInitializer>();

        return services;
    }

    /// <summary>
    /// Adds PostgreSQL event store and the necessary schema to the DI container.
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="config">Configuration section for PostgreSQL options</param>
    /// <param name="configureBuilder">Optional: function to configure the data source builder</param>
    /// <param name="connectionLifetime">Optional: lifetime of the connection, default is transient</param>
    /// <param name="dataSourceLifetime">Optional> lifetime of the data source, default is singleton</param>
    /// <returns>Services collection</returns>
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static IServiceCollection AddEventuousPostgres(
            this IServiceCollection                            services,
            IConfiguration                                     config,
            Action<IServiceProvider, NpgsqlDataSourceBuilder>? configureBuilder   = null,
            ServiceLifetime                                    connectionLifetime = ServiceLifetime.Transient,
            ServiceLifetime                                    dataSourceLifetime = ServiceLifetime.Singleton
        ) {
        services.Configure<PostgresStoreOptions>(config);
        services.AddSingleton<PostgresStoreOptions>(sp => sp.GetRequiredService<IOptions<PostgresStoreOptions>>().Value);

        services.AddNpgsqlDataSourceCore(
            sp => Ensure.NotEmptyString(sp.GetRequiredService<PostgresStoreOptions>().ConnectionString),
            (sp, builder) => {
                var options = sp.GetRequiredService<PostgresStoreOptions>();
                builder.MapComposite<NewPersistedEvent>(Schema.GetStreamMessageTypeName(options.Schema));
                configureBuilder?.Invoke(sp, builder);
            },
            connectionLifetime,
            dataSourceLifetime
        );

        services.AddSingleton<PostgresStore>();
        services.AddHostedService<SchemaInitializer>();

        return services;
    }

    static void AddNpgsqlDataSourceCore(
            this IServiceCollection                            services,
            Func<IServiceProvider, string>                     getConnectionString,
            Action<IServiceProvider, NpgsqlDataSourceBuilder>? configureDataSource,
            ServiceLifetime                                    connectionLifetime,
            ServiceLifetime                                    dataSourceLifetime
        ) {
        services.TryAdd(
            new ServiceDescriptor(
                typeof(NpgsqlDataSource),
                sp => {
                    var dataSourceBuilder = new NpgsqlDataSourceBuilder(getConnectionString(sp));
                    dataSourceBuilder.UseLoggerFactory(sp.GetService<ILoggerFactory>());
                    configureDataSource?.Invoke(sp, dataSourceBuilder);

                    return dataSourceBuilder.Build();
                },
                dataSourceLifetime
            )
        );

        services.TryAdd(
            new ServiceDescriptor(typeof(NpgsqlConnection), sp => sp.GetRequiredService<NpgsqlDataSource>().CreateConnection(), connectionLifetime)
        );
        services.TryAdd(new ServiceDescriptor(typeof(DbDataSource), sp => sp.GetRequiredService<NpgsqlDataSource>(), dataSourceLifetime));
        services.TryAdd(new ServiceDescriptor(typeof(DbConnection), sp => sp.GetRequiredService<NpgsqlConnection>(), connectionLifetime));
    }

    public static IServiceCollection AddPostgresCheckpointStore(this IServiceCollection services) {
        return services.AddCheckpointStore<PostgresCheckpointStore>(
            sp => {
                var ds                     = sp.GetRequiredService<NpgsqlDataSource>();
                var loggerFactory          = sp.GetService<ILoggerFactory>();
                var storeOptions           = sp.GetService<PostgresStoreOptions>();
                var checkpointStoreOptions = sp.GetService<IOptions<PostgresCheckpointStoreOptions>>();

                var schema = storeOptions?.Schema is not null and not Schema.DefaultSchema
                 && checkpointStoreOptions?.Value.Schema == Schema.DefaultSchema
                        ? storeOptions.Schema
                        : checkpointStoreOptions?.Value.Schema ?? Schema.DefaultSchema;

                return new(ds, schema, loggerFactory);
            }
        );
    }
}
