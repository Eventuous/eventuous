using Eventuous.Tests.Persistence.Base.Store;
using Eventuous.Tests.Postgres.Fixtures;

namespace Eventuous.Tests.Postgres.Store;

// ReSharper disable once UnusedType.Global
public class AppendEvents(IntegrationFixture fixture) : StoreAppendTests<IntegrationFixture>(fixture);
