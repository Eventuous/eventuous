using Eventuous.Tests.Persistence.Base.Store;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.EventStore.Store;

public static class StoreTests {
    public class Append(IntegrationFixture fixture) : StoreAppendTests<IntegrationFixture>(fixture);

    public class Read(IntegrationFixture fixture) : StoreReadTests<IntegrationFixture>(fixture);

    public class OtherMethods(IntegrationFixture fixture) : StoreOtherOpsTests<IntegrationFixture>(fixture);
}
