// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql;

public class SchemaInitializer(PostgresStoreOptions options, ILoggerFactory? loggerFactory = null) : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        if (!options.InitializeDatabase) return Task.CompletedTask;
        var dataSource = new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
        var schema = new Schema(options.Schema);
        return schema.CreateSchema(dataSource, loggerFactory?.CreateLogger<Schema>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
