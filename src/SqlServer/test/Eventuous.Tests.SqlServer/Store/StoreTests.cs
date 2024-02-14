using Eventuous.Tests.Persistence.Base.Store;
// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Store;

public class AppendEvents(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);

public class Read(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);
