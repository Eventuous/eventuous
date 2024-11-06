using Eventuous.Tests.Persistence.Base.Store;

// ReSharper disable UnusedType.Global

namespace Eventuous.Tests.Postgres.Store;

[InheritsTests]
[ClassDataSource<StoreFixture>]
public class Append(StoreFixture fixture) : StoreAppendTests<StoreFixture>(fixture);

[InheritsTests]
[ClassDataSource<StoreFixture>]
public class Read(StoreFixture fixture) : StoreReadTests<StoreFixture>(fixture);

[InheritsTests]
[ClassDataSource<StoreFixture>]
public class OtherMethods(StoreFixture fixture) : StoreOtherOpsTests<StoreFixture>(fixture);