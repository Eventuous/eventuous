using Eventuous.Tests.Persistence.Base.Store;
// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Store;

public class AppendEvents(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

public class ReadEvents(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);

public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);