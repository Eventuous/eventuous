// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;

namespace Eventuous.Postgresql.Extensions;

static class DataSourceExtensions {
    public static NpgsqlCommand GetCommand(this NpgsqlConnection connection, string sql, NpgsqlTransaction? transaction = null) {
        var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.CommandType = CommandType.Text;
        cmd.Transaction = transaction;
        return cmd;
    }

    public static NpgsqlCommand Add(this NpgsqlCommand command, string name, NpgsqlDbType type, object value) {
        command.Parameters.AddWithValue(name, type, value);
        return command;
    }

    public static NpgsqlCommand Add(this NpgsqlCommand command, string name, object value) {
        command.Parameters.AddWithValue(name, value);
        return command;
    }
}
