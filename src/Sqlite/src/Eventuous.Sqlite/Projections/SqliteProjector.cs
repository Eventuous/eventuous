// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Subscriptions.Context;
using EventHandler = Eventuous.Subscriptions.EventHandler;

namespace Eventuous.Sqlite.Projections;

/// <summary>
/// Base class for projectors that store read models in SQLite.
/// </summary>
public abstract class SqliteProjector(SqliteConnectionOptions options, ITypeMapper? mapper = null) : EventHandler(mapper) {
    readonly string _connectionString = Ensure.NotEmptyString(options.ConnectionString);

    /// <summary>
    /// Define how an event is converted to a SQLite command to update the read model using event data.
    /// </summary>
    /// <param name="handler">Function to synchronously create a SQLite command from the event context.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToSqlite<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx, GetCommand).NoContext());

        return;

        ValueTask<SqliteCommand> GetCommand(SqliteConnection connection, MessageConsumeContext<T> context) => new(handler(connection, context));
    }

    /// <summary>
    /// Define how an event is converted to a SQLite command to update the read model using event data.
    /// </summary>
    /// <param name="handler">Function to asynchronously create a SQLite command from the event context.</param>
    /// <typeparam name="T"></typeparam>
    protected void On<T>(ProjectToSqliteAsync<T> handler) where T : class
        => base.On<T>(async ctx => await Handle(ctx, handler).NoContext());

    async Task Handle<T>(MessageConsumeContext<T> context, ProjectToSqliteAsync<T> handler) where T : class {
        await using var connection = await ConnectionFactory.GetConnection(_connectionString, context.CancellationToken);

        var cmd = await handler(connection, context).ConfigureAwait(false);
        await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);
    }

    protected static SqliteCommand Project(SqliteConnection connection, string commandText, params SqliteParameter[] parameters) {
        var cmd = connection.CreateCommand();
        cmd.CommandText = commandText;
        cmd.Parameters.AddRange(parameters);
        cmd.CommandType = System.Data.CommandType.Text;

        return cmd;
    }
}

public delegate SqliteCommand ProjectToSqlite<T>(SqliteConnection connection, MessageConsumeContext<T> consumeContext) where T : class;

public delegate ValueTask<SqliteCommand> ProjectToSqliteAsync<T>(SqliteConnection connection, MessageConsumeContext<T> consumeContext) where T : class;
