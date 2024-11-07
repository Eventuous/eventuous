// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Postgresql.Extensions;
using Eventuous.Subscriptions.Context;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.Postgresql.Projections;

/// <summary>
/// Base class for projectors that store read models in PostgreSQL.
/// </summary>
public abstract class PostgresProjector(NpgsqlDataSource dataSource, ITypeMapper? mapper = null) : EventHandler(mapper) {
    protected void On<T>(ProjectToPostgres<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx, GetCommand).NoContext());

        return;

        ValueTask<NpgsqlCommand> GetCommand(NpgsqlConnection connection, MessageConsumeContext<T> context) => new(handler(connection, context));
    }

    /// <summary>
    /// Define what happens when a message is received.
    /// </summary>
    /// <param name="handler">Function to project the event to a read model in PostgreSQL.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToPostgresAsync<T> handler) where T : class => base.On<T>(async ctx => await Handle(ctx, handler).NoContext());

    async Task Handle<T>(MessageConsumeContext<T> context, ProjectToPostgresAsync<T> handler) where T : class {
        await using var connection = await dataSource.OpenConnectionAsync().NoContext();
        var             cmd        = await handler(connection, context).NoContext();
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).NoContext();
    }

    protected static NpgsqlCommand Project(NpgsqlConnection connection, string commandText, params NpgsqlParameter[] parameters) {
        var cmd = connection.GetCommand(commandText);
        cmd.Parameters.AddRange(parameters);

        return cmd;
    }
}

public delegate NpgsqlCommand ProjectToPostgres<T>(NpgsqlConnection connection, MessageConsumeContext<T> consumeContext) where T : class;

public delegate ValueTask<NpgsqlCommand> ProjectToPostgresAsync<T>(NpgsqlConnection connection, MessageConsumeContext<T> consumeContext) where T : class;
