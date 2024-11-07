// Copyright (C) Eventuous HQ OÜ. All rights reserved
// Licensed under the Apache License, Version 2.0.

using Eventuous.Sql.Base.Subscriptions;
using Eventuous.SqlServer.Projections;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Filters;
using Microsoft.Extensions.Logging;

namespace Eventuous.SqlServer.Subscriptions;

public abstract class SqlServerSubscriptionBase<T> : SqlSubscriptionBase<T, SqlConnection> where T : SqlServerSubscriptionBaseOptions {
    protected Schema Schema { get; }
    readonly  string _connectionString;

    protected SqlServerSubscriptionBase(
            T                           options,
            ICheckpointStore            checkpointStore,
            ConsumePipe                 consumePipe,
            SubscriptionKind            kind,
            ILoggerFactory?             loggerFactory,
            IEventSerializer?           eventSerializer,
            IMetadataSerializer?        metaSerializer,
            SqlServerConnectionOptions? connectionOptions
        ) : base(options, checkpointStore, consumePipe, options.ConcurrencyLimit, kind, loggerFactory, eventSerializer, metaSerializer) {
        Schema = new(
            connectionOptions?.Schema is not null and not Schema.DefaultSchema
                ? connectionOptions.Schema
                : options.Schema
        );
        _connectionString = Ensure.NotEmptyString(Options.ConnectionString);
        GetEndOfStream    = $"SELECT MAX(StreamPosition) FROM {options.Schema}.Messages";
        GetEndOfAll       = $"SELECT MAX(GlobalPosition) FROM {options.Schema}.Messages";
    }

    protected override async ValueTask<SqlConnection> OpenConnection(CancellationToken cancellationToken)
        => await ConnectionFactory.GetConnection(_connectionString, cancellationToken).NoContext();

    protected override bool IsTransient(Exception exception) => exception is SqlException sqlException && TransientErrorNumbers.Contains(sqlException.Number);

    // ReSharper disable once StaticMemberInGenericType

    static readonly List<int> TransientErrorNumbers = [4060, 40197, 40501, 40613, 49918, 49919, 49920, 11001];

    protected override bool IsStopping(Exception exception)
        => exception switch {
            OperationCanceledException => true,
            SqlException sqlException => sqlException.Message.Contains("Operation cancelled by user.") ||
                sqlException.Number == 3980 && sqlException.Message.Contains("Operation cancelled by user."),
            _ => false
        };

    protected override string GetEndOfStream { get; }
    protected override string GetEndOfAll    { get; }
}

public abstract record SqlServerSubscriptionBaseOptions : SqlSubscriptionOptionsBase {
    protected SqlServerSubscriptionBaseOptions() => Schema = SqlServer.Schema.DefaultSchema;
    public string? ConnectionString { get; set; }
}
