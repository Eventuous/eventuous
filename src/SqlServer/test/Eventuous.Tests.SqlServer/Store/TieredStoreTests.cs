using Eventuous.Tests.Persistence.Base.Store;
using JetBrains.Annotations;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Store;

[UsedImplicitly]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<SqlEdgeContainer>(storeFixture), IClassFixture<StoreFixture>;
