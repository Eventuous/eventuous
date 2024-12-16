using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Store;

[ClassDataSource<StoreFixture>(Shared = SharedType.PerClass)]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<MsSqlContainer>(storeFixture) {
    [Test]
    public async Task Should_load_hot_and_archive_test() {
        await Should_load_hot_and_archive();
    }
}
