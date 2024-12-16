// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer;

public class SchemaInitializer(SqlServerStoreOptions options, ILoggerFactory? loggerFactory = null) : IHostedService {
    readonly ILogger<Schema>? _log = loggerFactory?.CreateLogger<Schema>();

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (!options.InitializeDatabase) return;

        var schema           = new Schema(options.Schema);
        var connectionString = Ensure.NotEmptyString(options.ConnectionString);

        Exception? ex = null;

        for (var i = 0; i < 10; i++) {
            try {
                await schema.CreateSchema(connectionString, _log, cancellationToken);

                return;
            } catch (SqlException e) {
                _log?.LogError("Unable to initialize the database schema: {Message}", e.Message);
                ex = e;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        throw ex!;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
