using Eventuous.Tests.Persistence.Base.Store;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Store;

public static class StoreTests {
    public class Append(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

    public class Read(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);

    public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);
}
