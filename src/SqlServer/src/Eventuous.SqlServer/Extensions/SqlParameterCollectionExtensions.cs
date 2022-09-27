// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Microsoft.Data.SqlClient;

namespace Eventuous.SqlServer.Extensions;

internal static class SqlParameterCollectionExtensions {
    internal static SqlParameter AddPersistedEvent(this SqlParameterCollection parameters,
        string                                                                 parameterName,
        IEnumerable<NewPersistedEvent>                                         persistedEvents
    ) {
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

        return parameters.AddWithValue(parameterName, SqlDbType.Structured, tableVariable);
    }

    internal static SqlParameter AddOutput(this SqlParameterCollection parameters,
        string                                                         parameterName,
        SqlDbType                                                      sqlDbType) {
        return parameters.Add(new SqlParameter {
            ParameterName = parameterName,
            SqlDbType     = sqlDbType,
            Direction     = ParameterDirection.Output,
        });
    }

    internal static SqlParameter AddWithValue(this SqlParameterCollection parameters,
        string                                                            parameterName,
        SqlDbType                                                         sqlDbType,
        object                                                            value) {
        var param = parameters.AddWithValue(parameterName, value);
        param.SqlDbType = sqlDbType;
        return param;
    }
}