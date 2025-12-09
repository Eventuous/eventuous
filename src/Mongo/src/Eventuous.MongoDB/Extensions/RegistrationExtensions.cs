// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.MongoDB.Snapshots;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering MongoDB snapshot store.
/// </summary>
public static class ServiceCollectionExtensions {
    /// <param name="services">Service collection</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds MongoDB snapshot store to the DI container.
        /// </summary>
        /// <param name="connectionString">MongoDB connection string</param>
        /// <param name="databaseName">Database name. Default is "eventuous"</param>
        /// <param name="collectionName">Collection name. Default is "snapshots"</param>
        /// <param name="initializeIndexes">Set to true to initialize indexes on startup. Default is false</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddMongoSnapshotStore(
                string connectionString,
                string databaseName        = "eventuous",
                string collectionName      = "snapshots",
                bool   initializeIndexes    = false
            ) {
            var options = new MongoSnapshotStoreOptions {
                ConnectionString   = Ensure.NotEmptyString(connectionString),
                DatabaseName       = databaseName,
                CollectionName     = collectionName,
                InitializeIndexes  = initializeIndexes
            };

            services.AddSingleton(options);
            services.AddSingleton<MongoSnapshotStore>(sp => {
                var opts = sp.GetRequiredService<MongoSnapshotStoreOptions>();
                var mongoSettings = MongoClientSettings.FromConnectionString(opts.ConnectionString);
                var client        = new MongoClient(mongoSettings);
                var database      = client.GetDatabase(opts.DatabaseName);
                return new MongoSnapshotStore(database, opts, sp.GetService<IEventSerializer>());
            });
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<MongoSnapshotStore>());

            if (options.InitializeIndexes) {
                services.AddHostedService<SnapshotIndexInitializer>(sp => {
                    var opts = sp.GetRequiredService<MongoSnapshotStoreOptions>();
                    var mongoSettings = MongoClientSettings.FromConnectionString(opts.ConnectionString);
                    var client        = new MongoClient(mongoSettings);
                    var database      = client.GetDatabase(opts.DatabaseName);
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    return new SnapshotIndexInitializer(database, opts, loggerFactory);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds MongoDB snapshot store to the DI container using configuration.
        /// </summary>
        /// <param name="config">Configuration section for MongoDB snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddMongoSnapshotStore(IConfiguration config) {
            services.Configure<MongoSnapshotStoreOptions>(config);
            services.AddSingleton<MongoSnapshotStoreOptions>(sp => sp.GetRequiredService<IOptions<MongoSnapshotStoreOptions>>().Value);

            services.AddSingleton<MongoSnapshotStore>(sp => {
                var opts = sp.GetRequiredService<MongoSnapshotStoreOptions>();
                var mongoSettings = MongoClientSettings.FromConnectionString(Ensure.NotEmptyString(opts.ConnectionString));
                var client        = new MongoClient(mongoSettings);
                var database      = client.GetDatabase(opts.DatabaseName);
                return new MongoSnapshotStore(database, opts, sp.GetService<IEventSerializer>());
            });
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<MongoSnapshotStore>());

            // Register index initializer only if InitializeIndexes is enabled
            // The initializer will check the option and skip if false
            services.AddHostedService<SnapshotIndexInitializer>(sp => {
                var opts = sp.GetRequiredService<MongoSnapshotStoreOptions>();
                var mongoSettings = MongoClientSettings.FromConnectionString(Ensure.NotEmptyString(opts.ConnectionString));
                var client        = new MongoClient(mongoSettings);
                var database      = client.GetDatabase(opts.DatabaseName);
                var loggerFactory = sp.GetService<ILoggerFactory>();
                return new SnapshotIndexInitializer(database, opts, loggerFactory);
            });

            return services;
        }
    }
}

