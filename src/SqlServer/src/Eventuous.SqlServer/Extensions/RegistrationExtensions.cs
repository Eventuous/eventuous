// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.SqlServer;
using Eventuous.SqlServer.Projections;
using Eventuous.SqlServer.Snapshots;
using Eventuous.SqlServer.Subscriptions;
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
        /// Adds SQL Server event store and the necessary schema to the DI container.
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="schema">Schema name</param>
        /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlServer(
                string connectionString,
                string schema             = Schema.DefaultSchema,
                bool   initializeDatabase = false
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
        /// <param name="config">Configuration section for SQL Server options</param>
        /// <returns></returns>
        public IServiceCollection AddEventuousSqlServer(IConfiguration config) {
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
        /// <returns></returns>
        public IServiceCollection AddSqlServerCheckpointStore()
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

        /// <summary>
        /// Adds SQL Server snapshot store and the necessary schema to the DI container.
        /// </summary>
        /// <param name="connectionString">Connection string</param>
        /// <param name="schema">Schema name</param>
        /// <param name="initializeDatabase">Set to true if you want the schema to be created on startup</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddSqlServerSnapshotStore(
                string connectionString,
                string schema             = SnapshotSchema.DefaultSchema,
                bool   initializeDatabase = false
            ) {
            var options = new SqlServerSnapshotStoreOptions {
                Schema             = schema,
                ConnectionString   = connectionString,
                InitializeDatabase = initializeDatabase
            };

            services.AddSingleton(options);
            services.AddSingleton<SqlServerSnapshotStore>(sp => {
                var snapshotOptions = sp.GetRequiredService<SqlServerSnapshotStoreOptions>();
                return new SqlServerSnapshotStore(snapshotOptions.ConnectionString, snapshotOptions, sp.GetService<IEventSerializer>());
            });
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<SqlServerSnapshotStore>());
            services.AddHostedService<SnapshotSchemaInitializer>();

            return services;
        }

        /// <summary>
        /// Adds SQL Server snapshot store and the necessary schema to the DI container using the configuration.
        /// </summary>
        /// <param name="config">Configuration section for SQL Server snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddSqlServerSnapshotStore(IConfiguration config) {
            services.Configure<SqlServerSnapshotStoreOptions>(config);
            services.AddSingleton<SqlServerSnapshotStoreOptions>(sp => sp.GetRequiredService<IOptions<SqlServerSnapshotStoreOptions>>().Value);

            services.AddSingleton<SqlServerSnapshotStore>(sp => {
                var options = sp.GetRequiredService<SqlServerSnapshotStoreOptions>();
                return new SqlServerSnapshotStore(options.ConnectionString, options, sp.GetService<IEventSerializer>());
            });
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<SqlServerSnapshotStore>());
            services.AddHostedService<SnapshotSchemaInitializer>();

            return services;
        }
    }
}
