using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Store;

[InheritsTests]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<PostgreSqlContainer>(storeFixture);
