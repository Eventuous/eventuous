// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

namespace Eventuous.Sqlite.Extensions;

static class SqliteExtensions {
    extension(SqliteCommand command) {
        internal SqliteCommand Add(string parameterName, object? value) {
            command.Parameters.AddWithValue(parameterName, value ?? DBNull.Value);

            return command;
        }
    }

    extension(SqliteConnection connection) {
        internal SqliteCommand GetTextCommand(string sql, SqliteTransaction? transaction = null) {
            var cmd = connection.CreateCommand();
            cmd.CommandType = System.Data.CommandType.Text;
            cmd.CommandText = sql;
            if (transaction != null) cmd.Transaction = transaction;

            return cmd;
        }
    }
}
