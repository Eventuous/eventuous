using Eventuous.Sqlite;
using Eventuous.Tests.Sqlite.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Eventuous.Tests.Sqlite.Store;

public sealed class StoreFixture() : SqliteStoreFixtureBase() {
    readonly string _schemaName = GetSchemaName();

    protected override void SetupServices(IServiceCollection services) {
        services.AddEventuousSqlite(ConnectionString, _schemaName, true);
        services.AddEventStore<SqliteStore>();
    }
}
