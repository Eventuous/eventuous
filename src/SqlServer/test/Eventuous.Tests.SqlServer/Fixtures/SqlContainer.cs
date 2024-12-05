using Testcontainers.MsSql;

namespace Eventuous.Tests.SqlServer.Fixtures;

public static class SqlContainer {
    public static MsSqlContainer Create() => new MsSqlBuilder().WithImage("mcr.microsoft.com/mssql/server:2022-latest").Build();
}
