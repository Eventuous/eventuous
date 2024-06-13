using Eventuous.Tests.Persistence.Base.Store;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.SqlServer.Store;

[Collection("Database")]
public class Append(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

[Collection("Database")]
public class Read(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);

[Collection("Database")]
public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);
