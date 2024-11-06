using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Store;

public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<PostgreSqlContainer>(storeFixture);
