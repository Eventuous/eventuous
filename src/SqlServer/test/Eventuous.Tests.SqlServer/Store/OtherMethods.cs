using Eventuous.Tests.Persistence.Base.Store;
using Eventuous.Tests.SqlServer.Fixtures;

namespace Eventuous.Tests.SqlServer.Store;

// ReSharper disable once UnusedType.Global
public class OtherMethods(IntegrationFixture fixture) : StoreOtherOpsTests<IntegrationFixture>(fixture);
