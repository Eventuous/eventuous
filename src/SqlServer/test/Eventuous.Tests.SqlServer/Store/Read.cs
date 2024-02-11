using Eventuous.Tests.Persistence.Base.Store;
using Eventuous.Tests.SqlServer.Fixtures;

namespace Eventuous.Tests.SqlServer.Store;

// ReSharper disable once UnusedType.Global
public class Read(IntegrationFixture fixture) : StoreReadTests<IntegrationFixture>(fixture);
