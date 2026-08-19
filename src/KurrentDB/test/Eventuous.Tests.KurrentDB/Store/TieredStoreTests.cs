using Eventuous.Tests.Persistence.Base.Store;
using Testcontainers.KurrentDb;

namespace Eventuous.Tests.KurrentDB.Store;

[ClassDataSource<StoreFixture>]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<KurrentDbContainer>(storeFixture) {
    [Test]
    public async Task Esdb_should_load_hot_and_archive() {
        await Should_load_hot_and_archive();
    }

    [Test]
    public async Task Esdb_should_return_empty_reading_past_end() {
        await Should_return_empty_reading_past_end();
    }

    [Test]
    public async Task Esdb_should_read_stream_to_end_with_exact_page_multiple() {
        await Should_read_stream_to_end_with_exact_page_multiple();
    }
}