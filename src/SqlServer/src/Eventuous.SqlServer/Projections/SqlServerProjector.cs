// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.SqlServer.Projections;

/// <summary>
/// Base class for projectors that store read models in SQL Server.
/// </summary>
public abstract class SqlServerProjector(SqlServerConnectionOptions options, ITypeMapper? mapper = null) : EventHandler(mapper) {
    readonly string _connectionString = Ensure.NotEmptyString(options.ConnectionString);

    /// <summary>
    /// Define how an event is converted to an SQL Server command to update the read model using event data.
    /// </summary>
    /// <param name="handler">Function to synchronously create an SQL Server command from the event context.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToSqlServer<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx, GetCommand).NoContext());

        return;

        ValueTask<SqlCommand> GetCommand(SqlConnection connection, MessageConsumeContext<T> context) => new(handler(connection, context));
    }

    /// <summary>
    /// Define how an event is converted to an SQL Server command to update the read model using event data.
    /// </summary>
    /// <param name="handler">Function to asynchronously create an SQL Server command from the event context.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToSqlServerAsync<T> handler) where T : class
        => base.On<T>(async ctx => await Handle(ctx, handler).NoContext());

    async Task Handle<T>(MessageConsumeContext<T> context, ProjectToSqlServerAsync<T> handler) where T : class {
        await using var connection = await ConnectionFactory.GetConnection(_connectionString, context.CancellationToken);

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

public delegate SqlCommand ProjectToSqlServer<T>(SqlConnection connection, MessageConsumeContext<T> consumeContext) where T : class;

public delegate ValueTask<SqlCommand> ProjectToSqlServerAsync<T>(SqlConnection connection, MessageConsumeContext<T> consumeContext) where T : class;
