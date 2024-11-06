using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Store;

[ClassDataSource<StoreFixture>(Shared = SharedType.ForClass)]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<SqlEdgeContainer>(storeFixture) {
    [Test]
    public async Task Should_load_hot_and_archive_test() {
        await Should_load_hot_and_archive();
    }
}
