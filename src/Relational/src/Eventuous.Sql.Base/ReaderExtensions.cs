// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data.Common;
using System.Runtime.CompilerServices;

namespace Eventuous.Sql.Base;

/// <summary>
/// Extensions for working with ADO.NET DbDataReader
/// </summary>
public static class ReaderExtensions {
    /// <summary>
    /// Read events from the reader
    /// </summary>
    /// <param name="reader">Reader of the SQL SELECT statement for messages table</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Async enumerable with PersistentEvent collection</returns>
    public static async IAsyncEnumerable<PersistedEvent> ReadEvents(
            this                     DbDataReader      reader,
            [EnumeratorCancellation] CancellationToken cancellationToken
        ) {
        while (await reader.ReadAsync(cancellationToken).NoContext()) {
            var evt = new PersistedEvent(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt64(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDateTime(6),
                reader.FieldCount >= 8 ? reader.GetString(7) : null
            );

            yield return evt;
        }
    }
}
