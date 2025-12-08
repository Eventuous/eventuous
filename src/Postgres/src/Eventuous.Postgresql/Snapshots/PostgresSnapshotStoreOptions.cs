// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace Eventuous.Postgresql.Snapshots;

public class PostgresSnapshotStoreOptions(string schema) {
    public PostgresSnapshotStoreOptions() : this(SnapshotSchema.DefaultSchema) { }

    /// <summary>
    /// Override the default schema name.
    /// </summary>
    public string Schema { get; set; } = schema;

    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string ConnectionString { get; set; } = null!;

    /// <summary>
    /// Set to true to initialize the database schema on startup. Default is false.
    /// </summary>
    public bool InitializeDatabase { get; set; }
}

