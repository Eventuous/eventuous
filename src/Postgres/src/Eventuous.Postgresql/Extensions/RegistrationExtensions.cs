// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using Eventuous.Postgresql;
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
    /// <param name="connectionLifetime">Optional: lifetime of the connection, default is transient</param>
    /// <param name="dataSourceLifetime">Optional> lifetime of the data source, default is singleton</param>
    /// <returns></returns>
    public static IServiceCollection AddEventuousPostgres(
            this IServiceCollection services,
            string                  connectionString,
            string                  schema,
            bool                    initializeDatabase = false,
            ServiceLifetime         connectionLifetime = ServiceLifetime.Transient,
            ServiceLifetime         dataSourceLifetime = ServiceLifetime.Singleton
        ) {
        var options = new PostgresStoreOptions {
            Schema             = schema,
            ConnectionString   = connectionString,
            InitializeDatabase = initializeDatabase
        };

        var s = new Schema(schema);

        services.AddNpgsqlDataSourceCore(
            _ => connectionString,
            (_, builder) => builder.MapComposite<NewPersistedEvent>(s.StreamMessage),
            connectionLifetime,
            dataSourceLifetime
        );

        services.AddSingleton<PostgresStore>(
            sp => {
                var dataSource      = sp.GetRequiredService<NpgsqlDataSource>();
                var eventSerializer = sp.GetService<IEventSerializer>();
                var metaSerializer  = sp.GetService<IMetadataSerializer>();

                return new PostgresStore(dataSource, options, eventSerializer, metaSerializer);
            }
        );

        if (initializeDatabase) {
            services.AddHostedService<SchemaInitializer>(
                sp => {
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new SchemaInitializer(options, loggerFactory);
                }
            );
        }

        return services;
    }

    public static IServiceCollection AddEventuousPostgres(
            this IServiceCollection services,
            IConfiguration          config,
            ServiceLifetime         connectionLifetime = ServiceLifetime.Transient,
            ServiceLifetime         dataSourceLifetime = ServiceLifetime.Singleton
        ) {
        services.Configure<PostgresStoreOptions>(config);

        services.AddNpgsqlDataSourceCore(
            sp => sp.GetRequiredService<IOptions<PostgresStoreOptions>>().Value.ConnectionString,
            (sp, builder) => {
                var options = sp.GetRequiredService<IOptions<PostgresStoreOptions>>().Value;
                var schema  = new Schema(options.Schema);
                builder.MapComposite<NewPersistedEvent>(schema.StreamMessage);
            },
            connectionLifetime,
            dataSourceLifetime
        );

        services.AddSingleton<PostgresStore>(
            sp => {
                var dataSource      = sp.GetRequiredService<NpgsqlDataSource>();
                var eventSerializer = sp.GetService<IEventSerializer>();
                var metaSerializer  = sp.GetService<IMetadataSerializer>();
                var options         = sp.GetRequiredService<IOptions<PostgresStoreOptions>>();

                return new PostgresStore(dataSource, options.Value, eventSerializer, metaSerializer);
            }
        );

        if (config.GetValue<bool>("postgres:initializeDatabase") == true) {
            services.AddHostedService<SchemaInitializer>(
                sp => {
                    sp.GetRequiredService<NpgsqlDataSource>();
                    var options = sp.GetRequiredService<IOptions<PostgresStoreOptions>>();

                    return new SchemaInitializer(options.Value, sp.GetRequiredService<ILoggerFactory>());
                }
            );
        }

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
}
