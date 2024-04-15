using Testcontainers.PostgreSql;

namespace Eventuous.Tests.Postgres.Fixtures;

public static class PostgresContainer {
    public static PostgreSqlContainer Create()
        => new PostgreSqlBuilder()
            .WithUsername("postgres")
            .WithPassword("secret")
            .WithDatabase("eventuous")
            .Build();
}
