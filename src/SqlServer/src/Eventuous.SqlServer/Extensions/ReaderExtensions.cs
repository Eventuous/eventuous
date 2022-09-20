// Copyright (C) 2021-2022 Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;
using Microsoft.Data.SqlClient;

namespace Eventuous.SqlServer.Extensions;

static class ReaderExtensions {
    public static async IAsyncEnumerable<PersistedEvent> ReadEvents(
        this                     SqlDataReader     reader,
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
