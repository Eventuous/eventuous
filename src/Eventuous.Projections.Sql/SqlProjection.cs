using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Dapper;
using Eventuous.Subscriptions;

namespace Eventuous.Projections.Sql
{
    [PublicAPI]
    public abstract class SqlProjection : IEventHandler 
    {
        readonly ILogger? _log;
        readonly Func<IDbConnection> _getConnection;
        protected SqlProjection(Func<IDbConnection> getConnection, string subscriptionGroup, ILoggerFactory? loggerFactory) {
            _getConnection = getConnection;
            var log = loggerFactory?.CreateLogger(GetType());
            _log = log?.IsEnabled(LogLevel.Debug) == true ? log : null;
            SubscriptionId = Ensure.NotEmptyString(subscriptionGroup, nameof(subscriptionGroup));
        }

        public string SubscriptionId { get; }
        public async Task HandleEvent(object evt, long? position, CancellationToken cancellationToken) {
            var operation = GetUpdate(evt);

            if (operation == null) {
                _log?.LogDebug("No handler for {Event}", evt.GetType().Name);
                return;
            }

            _log?.LogDebug("Projecting {Event}", evt.GetType().Name);            

            await _getConnection().ExecuteAsync(operation.sql, operation.parameters);
        }
        protected abstract Operation GetUpdate(object evt);
    }

    public record Operation(object parameters, string sql);
}
