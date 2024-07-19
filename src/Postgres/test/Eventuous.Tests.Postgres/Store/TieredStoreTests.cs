using Eventuous.Tests.Persistence.Base.Store;
using JetBrains.Annotations;
using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Store;

[UsedImplicitly]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<PostgreSqlContainer>(storeFixture), IClassFixture<StoreFixture>;
