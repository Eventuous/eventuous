// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Redis;
using Eventuous.Redis.Snapshots;
using Microsoft.Extensions.Configuration;

// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class ServiceCollectionExtensions {
    /// <param name="services">Service collection</param>
    extension(IServiceCollection services) {
        /// <summary>
        /// Adds Redis snapshot store to the DI container.
        /// </summary>
        /// <param name="getDatabase">Function to get Redis database instance</param>
        /// <param name="options">Snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(
                GetRedisDatabase           getDatabase,
                RedisSnapshotStoreOptions? options
            ) {
            var snapshotOptions = options ?? new RedisSnapshotStoreOptions();
            services.AddSingleton(snapshotOptions);

            services.AddSingleton<RedisSnapshotStore>(sp => {
                    var redisSnapshotStoreOptions = sp.GetRequiredService<RedisSnapshotStoreOptions>();

                    return new(getDatabase, redisSnapshotStoreOptions, sp.GetService<IEventSerializer>());
                }
            );
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisSnapshotStore>());

            return services;
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container with default options.
        /// </summary>
        /// <param name="getDatabase">Function to get Redis database instance</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(GetRedisDatabase getDatabase) {
            return services.AddRedisSnapshotStore(getDatabase, (RedisSnapshotStoreOptions?)null);
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection multiplexer.
        /// </summary>
        /// <param name="connectionMultiplexer">Redis connection multiplexer</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <param name="options">Snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(
                IConnectionMultiplexer     connectionMultiplexer,
                int                        database,
                RedisSnapshotStoreOptions? options
            ) {
            return services.AddRedisSnapshotStore(GetDatabase, options);

            IDatabase GetDatabase() => connectionMultiplexer.GetDatabase(database);
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection multiplexer with default options.
        /// </summary>
        /// <param name="connectionMultiplexer">Redis connection multiplexer</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(IConnectionMultiplexer connectionMultiplexer, int database = 0) {
            return services.AddRedisSnapshotStore(connectionMultiplexer, database, null);
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using configuration.
        /// </summary>
        /// <param name="getDatabase">Function to get Redis database instance</param>
        /// <param name="config">Configuration section for Redis snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(GetRedisDatabase getDatabase, IConfiguration config) {
            var options = new RedisSnapshotStoreOptions();

            if (config[KeyPrefix] != null) {
                options.KeyPrefix = config[KeyPrefix]!;
            }

            services.AddSingleton(options);

            services.AddSingleton<RedisSnapshotStore>(sp => {
                    var snapshotOptions = sp.GetRequiredService<RedisSnapshotStoreOptions>();

                    return new(getDatabase, snapshotOptions, sp.GetService<IEventSerializer>());
                }
            );
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisSnapshotStore>());

            return services;
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection multiplexer and configuration.
        /// </summary>
        /// <param name="connectionMultiplexer">Redis connection multiplexer</param>
        /// <param name="config">Configuration section for Redis snapshot store options</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(
                IConnectionMultiplexer connectionMultiplexer,
                IConfiguration         config,
                int                    database = 0
            ) {
            var options = new RedisSnapshotStoreOptions();

            if (config[KeyPrefix] != null) {
                options.KeyPrefix = config[KeyPrefix]!;
            }

            services.AddSingleton(options);

            GetRedisDatabase getDatabase = () => connectionMultiplexer.GetDatabase(database);

            services.AddSingleton<RedisSnapshotStore>(sp => {
                    var snapshotOptions = sp.GetRequiredService<RedisSnapshotStoreOptions>();

                    return new(getDatabase, snapshotOptions, sp.GetService<IEventSerializer>());
                }
            );
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisSnapshotStore>());

            return services;
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection string.
        /// </summary>
        /// <param name="connectionString">Redis connection string</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <param name="options">Snapshot store options</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(
                string                     connectionString,
                int                        database,
                RedisSnapshotStoreOptions? options
            ) {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));

            var snapshotOptions = options ?? new RedisSnapshotStoreOptions();
            services.AddSingleton(snapshotOptions);

            services.AddSingleton<RedisSnapshotStore>(sp => {
                    var redisSnapshotStoreOptions = sp.GetRequiredService<RedisSnapshotStoreOptions>();
                    var muxer                     = sp.GetRequiredService<IConnectionMultiplexer>();

                    return new(GetDatabase, redisSnapshotStoreOptions, sp.GetService<IEventSerializer>());

                    IDatabase GetDatabase() => muxer.GetDatabase(database);
                }
            );
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisSnapshotStore>());

            return services;
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection string with default options.
        /// </summary>
        /// <param name="connectionString">Redis connection string</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(string connectionString, int database = 0) {
            return services.AddRedisSnapshotStore(connectionString, database, null);
        }

        /// <summary>
        /// Adds Redis snapshot store to the DI container using connection string and configuration.
        /// </summary>
        /// <param name="connectionString">Redis connection string</param>
        /// <param name="config">Configuration section for Redis snapshot store options</param>
        /// <param name="database">Database number (default: 0)</param>
        /// <returns>Services collection</returns>
        // ReSharper disable once UnusedMethodReturnValue.Global
        public IServiceCollection AddRedisSnapshotStore(
                string         connectionString,
                IConfiguration config,
                int            database = 0
            ) {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(connectionString));

            var options = new RedisSnapshotStoreOptions();

            if (config[KeyPrefix] != null) {
                options.KeyPrefix = config[KeyPrefix]!;
            }

            services.AddSingleton(options);

            services.AddSingleton<RedisSnapshotStore>(sp => {
                    var snapshotOptions = sp.GetRequiredService<RedisSnapshotStoreOptions>();
                    var muxer           = sp.GetRequiredService<IConnectionMultiplexer>();

                    return new(GetDatabase, snapshotOptions, sp.GetService<IEventSerializer>());

                    IDatabase GetDatabase() => muxer.GetDatabase(database);
                }
            );
            services.AddSingleton<ISnapshotStore>(sp => sp.GetRequiredService<RedisSnapshotStore>());

            return services;
        }
    }

    const string KeyPrefix = "KeyPrefix";
}
