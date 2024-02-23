using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Fixtures;

public static class SqlContainer {
    public static SqlEdgeContainer Create() => new SqlEdgeBuilder()
        .WithImage("mcr.microsoft.com/azure-sql-edge:latest")
        .Build();
}
