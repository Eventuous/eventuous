// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Snapshots;

public class SnapshotSchemaInitializer(SqlServerSnapshotStoreOptions options, ILoggerFactory? loggerFactory = null) : IHostedService {
    readonly ILogger<SnapshotSchema>? _log = loggerFactory?.CreateLogger<SnapshotSchema>();

    public async Task StartAsync(CancellationToken cancellationToken) {
        if (!options.InitializeDatabase) return;

        var schema           = new SnapshotSchema(options.Schema);
        var connectionString = Ensure.NotEmptyString(options.ConnectionString);

        Exception? ex = null;

        for (var i = 0; i < 10; i++) {
            try {
                await schema.CreateSchema(connectionString, _log, cancellationToken);

                return;
            } catch (SqlException e) {
                _log?.LogError("Unable to initialize the snapshot database schema: {Message}", e.Message);
                ex = e;
            }

            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        }

        throw ex!;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

