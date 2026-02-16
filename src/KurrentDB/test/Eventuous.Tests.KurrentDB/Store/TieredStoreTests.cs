using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Store;

[ClassDataSource<StoreFixture>]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<KurrentDbContainer>(storeFixture) {
    [Test]
    public async Task Esdb_should_load_hot_and_archive() {
        await Should_load_hot_and_archive();
    }
}