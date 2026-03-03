// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sqlite;
using Eventuous.Sqlite.Projections;
using Eventuous.Sqlite.Subscriptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    /// <param name="services">Service collection</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds SQLite event store and the necessary schema to the DI container.
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="schema">Schema name</param>
        /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlite(
                string connectionString,
                string schema             = Schema.DefaultSchema,
                bool   initializeDatabase = false
            ) {
            var options = new SqliteStoreOptions {
                Schema             = Ensure.NotEmptyString(schema),
                ConnectionString   = Ensure.NotEmptyString(connectionString),
                InitializeDatabase = initializeDatabase
            };
            services.AddSingleton(options);
            services.AddSingleton<SqliteStore>();
            services.AddHostedService<SchemaInitializer>();
            services.TryAddSingleton(new SqliteConnectionOptions(connectionString, schema));

            return services;
        }

        /// <summary>
        /// Adds SQLite event store and the necessary schema to the DI container using the configuration.
        /// </summary>
        /// <param name="config">Configuration section for SQLite options</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlite(IConfiguration config) {
            services.Configure<SqliteStoreOptions>(config);
            services.AddSingleton<SqliteStoreOptions>(sp => sp.GetRequiredService<IOptions<SqliteStoreOptions>>().Value);
            services.AddSingleton<SqliteStore>();
            services.AddHostedService<SchemaInitializer>();

            services.TryAddSingleton(
                sp => {
                    var storeOptions = sp.GetRequiredService<IOptions<SqliteStoreOptions>>().Value;

                    return new SqliteConnectionOptions(Ensure.NotEmptyString(storeOptions.ConnectionString), storeOptions.Schema);
                }
            );

            return services;
        }

        /// <summary>
        /// Registers the SQLite-based checkpoint store using the details provided when registering
        /// SQLite connection factory.
        /// </summary>
        /// <returns></returns>
        public IServiceCollection AddSqliteCheckpointStore()
            => services.AddCheckpointStore<SqliteCheckpointStore>(
                sp => {
                    var loggerFactory          = sp.GetService<ILoggerFactory>();
                    var connectionOptions      = sp.GetService<SqliteConnectionOptions>();
                    var checkpointStoreOptions = sp.GetService<SqliteCheckpointStoreOptions>();

                    var schema = connectionOptions?.Schema is not null and not Schema.DefaultSchema
                     && checkpointStoreOptions?.Schema is null or Schema.DefaultSchema
                            ? connectionOptions.Schema
                            : checkpointStoreOptions?.Schema ?? Schema.DefaultSchema;
                    var connectionString = checkpointStoreOptions?.ConnectionString ?? connectionOptions?.ConnectionString;

                    return new(Ensure.NotNull(connectionString), schema, loggerFactory);
                }
            );
    }
}
