using Testcontainers.SqlEdge;

namespace Eventuous.Tests.SqlServer.Fixtures;

public static class SqlContainer {
    public static SqlEdgeContainer Create() => new SqlEdgeBuilder()
        // .WithImage("mcr.microsoft.com/azure-sql-edge:1.0.7")
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();
}
