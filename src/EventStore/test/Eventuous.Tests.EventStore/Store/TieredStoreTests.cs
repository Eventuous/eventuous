using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Store;

[ClassDataSource<StoreFixture>]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<EventStoreDbContainer>(storeFixture) {
    [Test]
    public async Task Esdb_should_load_hot_and_archive() {
        await Should_load_hot_and_archive();
    }
}
