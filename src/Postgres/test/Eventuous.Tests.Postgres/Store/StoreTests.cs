using Eventuous.Tests.Persistence.Base.Store;
// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Store;

[Collection("Database")]
public class AppendEvents(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

[Collection("Database")]
public class ReadEvents(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);

[Collection("Database")]
public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);