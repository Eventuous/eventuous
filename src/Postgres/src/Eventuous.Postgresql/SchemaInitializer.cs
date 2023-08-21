// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.Postgresql;

public class SchemaInitializer(PostgresStoreOptions options, ILoggerFactory loggerFactory) : IHostedService {
    public async Task StartAsync(CancellationToken cancellationToken) {
        var dataSource = new NpgsqlDataSourceBuilder(options.ConnectionString).Build();
        var schema = new Schema(options.Schema);
        await schema.CreateSchema(dataSource, loggerFactory.CreateLogger<Schema>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
