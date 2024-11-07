// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer;

public class SchemaInitializer(SqlServerStoreOptions options, ILoggerFactory? loggerFactory = null) : IHostedService {
    public Task StartAsync(CancellationToken cancellationToken) {
        if (!options.InitializeDatabase) return Task.CompletedTask;

        var schema           = new Schema(options.Schema);
        var connectionString = Ensure.NotEmptyString(options.ConnectionString);

        return schema.CreateSchema(connectionString, loggerFactory?.CreateLogger<Schema>(), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
