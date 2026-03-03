// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Sqlite;

delegate Task<SqliteConnection> GetSqliteConnection(CancellationToken cancellationToken);

public static class ConnectionFactory {
    public static async Task<SqliteConnection> GetConnection(string connectionString, CancellationToken cancellationToken) {
        var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).NoContext();

        await using var walCmd = connection.CreateCommand();
        walCmd.CommandText = "PRAGMA journal_mode=WAL";
        await walCmd.ExecuteNonQueryAsync(cancellationToken).NoContext();

        return connection;
    }
}
