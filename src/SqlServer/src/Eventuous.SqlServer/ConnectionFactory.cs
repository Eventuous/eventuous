// Copyright (C) Eventuous HQ OÜ.All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.SqlServer;

delegate Task<SqlConnection> GetSqlServerConnection(CancellationToken cancellationToken);

public static class ConnectionFactory {
    public static async Task<SqlConnection> GetConnection(string connectionString, CancellationToken cancellationToken) {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).NoContext();
        return connection;
    }
}
