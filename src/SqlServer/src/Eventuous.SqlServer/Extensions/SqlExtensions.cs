// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base;

namespace Eventuous.SqlServer.Extensions;

static class SqlExtensions {
    internal static SqlCommand AddPersistedEvent(this SqlCommand command, string parameterName, IEnumerable<NewPersistedEvent> persistedEvents) {
        var tableVariable = new DataTable();

        tableVariable.Columns.Add("message_id", typeof(Guid));
        tableVariable.Columns.Add("message_type", typeof(string));
        tableVariable.Columns.Add("json_data", typeof(string));
        tableVariable.Columns.Add("json_metadata", typeof(string));

        foreach (var persistedEvent in persistedEvents) {
            var row = tableVariable.NewRow();
            row["message_id"]    = persistedEvent.MessageId;
            row["message_type"]  = persistedEvent.MessageType;
            row["json_data"]     = persistedEvent.JsonData;
            row["json_metadata"] = persistedEvent.JsonMetadata;
            tableVariable.Rows.Add(row);
        }

        return command.Add(parameterName, SqlDbType.Structured, tableVariable);
    }

    internal static SqlParameter AddOutputParameter(this SqlCommand command, string parameterName, SqlDbType sqlDbType)
        => command.Parameters.Add(new() { ParameterName = parameterName, SqlDbType = sqlDbType, Direction = ParameterDirection.Output });

    internal static SqlCommand AddOutput(this SqlCommand command, string parameterName, SqlDbType sqlDbType) {
        command.Parameters.Add(new() { ParameterName = parameterName, SqlDbType = sqlDbType, Direction = ParameterDirection.Output });

        return command;
    }

    internal static SqlCommand Add(this SqlCommand command, string parameterName, SqlDbType sqlDbType, object value) {
        var param = command.Parameters.AddWithValue(parameterName, value);
        param.SqlDbType = sqlDbType;

        return command;
    }

    internal static SqlCommand GetTextCommand(this SqlConnection connection, string sql) {
        var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = sql;

        return cmd;
    }

    internal static SqlCommand GetStoredProcCommand(this SqlConnection connection, string storedProcName, SqlTransaction? transaction = null) {
        var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.CommandText = storedProcName;
        if (transaction != null) cmd.Transaction = transaction;

        return cmd;
    }
}
