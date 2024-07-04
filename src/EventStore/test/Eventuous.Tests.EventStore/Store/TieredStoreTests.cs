using Eventuous.Tests.Persistence.Base.Store;
using JetBrains.Annotations;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Store;

[UsedImplicitly]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<EventStoreDbContainer>(storeFixture), IClassFixture<StoreFixture> {
    [Fact]
    public async Task Esdb_should_load_hot_and_archive() {
        await Should_load_hot_and_archive();
    }
}
