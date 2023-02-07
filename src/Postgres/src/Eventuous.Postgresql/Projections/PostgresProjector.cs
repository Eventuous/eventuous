// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Context;
using Eventuous.Tools;
using Npgsql;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.Postgresql.Projections;

public abstract class PostgresProjector : EventHandler {
    readonly GetPostgresConnection _getConnection;

    protected PostgresProjector(GetPostgresConnection getConnection, TypeMapper? mapper = null) : base(mapper) {
        _getConnection = getConnection;
    }

    protected void On<T>(ProjectToPostgres<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx).NoContext());

        async Task Handle(MessageConsumeContext<T> context) {
            await using var connection = _getConnection();
            await connection.OpenAsync(context.CancellationToken).NoContext();
            var cmd = await handler(connection, context).NoContext();
            await cmd.ExecuteNonQueryAsync(context.CancellationToken).NoContext();
        }
    }
}

public delegate Task<NpgsqlCommand> ProjectToPostgres<T>(NpgsqlConnection connection, MessageConsumeContext<T> consumeContext)
    where T : class;
