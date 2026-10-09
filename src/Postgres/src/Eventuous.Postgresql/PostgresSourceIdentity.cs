// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Runtime.CompilerServices;

namespace Eventuous.Postgresql;

// Attach identities only to data sources built by a known-equivalent registration.
// Weak keys avoid extending data-source lifetimes; unregistered sources remain independent.
static class PostgresSourceIdentity {
    static readonly ConditionalWeakTable<NpgsqlDataSource, Identity> Identities = new();

    public static void Register(NpgsqlDataSource source, object registration, string connectionString)
        => Identities.Add(source, new(registration, connectionString));

    public static object Get(NpgsqlDataSource source)
        => Identities.TryGetValue(source, out var identity) ? identity : source;

    sealed record Identity(object Registration, string ConnectionString);
}
