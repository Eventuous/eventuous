using Eventuous.Tests.Persistence.Base.Store;
using JetBrains.Annotations;
using Testcontainers.EventStoreDb;

namespace Eventuous.Tests.EventStore.Store;

[UsedImplicitly]
public class TieredStoreTests(StoreFixture storeFixture) : TieredStoreTestsBase<EventStoreDbContainer>(storeFixture), IClassFixture<StoreFixture>;
