// Copyright (C) Ubiquitous AS.All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.SqlServer.Projections;
/// <summary>
/// Base class for projectors that store read models in SQL Server.
/// </summary>
public abstract class SqlServerProjector(string connectionString, TypeMapper? mapper = null) : EventHandler(mapper) {
    protected void On<T>(ProjectToSqlServer<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx, GetCommand).ConfigureAwait(false));

        return;

        ValueTask<SqlCommand> GetCommand(SqlConnection connection, MessageConsumeContext<T> context)
            => new(handler(connection, context));
    }

    /// <summary>
    /// Define what happens when a message is received.
    /// </summary>
    /// <param name="handler">Function to project the event to a read model in SQL Server.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToSqlServerAsync<T> handler) where T : class
        => base.On<T>(async ctx => await Handle(ctx, handler).ConfigureAwait(false));

    async Task Handle<T>(MessageConsumeContext<T> context, ProjectToSqlServerAsync<T> handler) where T : class {
        await using var connection = await ConnectionFactory.GetConnection(connectionString, context.CancellationToken);
        var cmd = await handler(connection, context).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);
    }

    protected static SqlCommand Project(SqlConnection connection, string commandText, params SqlParameter[] parameters) {
        var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.Parameters.AddRange(parameters);
        cmd.CommandType = CommandType.Text;

        return cmd;
    }
}

public delegate SqlCommand ProjectToSqlServer<T>(SqlConnection connection, MessageConsumeContext<T> consumeContext)
    where T : class;

public delegate ValueTask<SqlCommand> ProjectToSqlServerAsync<T>(SqlConnection connection, MessageConsumeContext<T> consumeContext)
    where T : class;
