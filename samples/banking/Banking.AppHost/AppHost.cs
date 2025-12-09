var builder = DistributedApplication.CreateBuilder(args);

var kurrentdb = builder.AddKurrentDB("eventuous-kurrentdb", 2113)
    .WithEnvironment("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true");

var postgres = builder.AddPostgres("eventuous-postgres")
    .WithPgWeb();

var postgresSnapshotsDb = postgres.AddDatabase("snapshots");

var sqlServer = builder.AddSqlServer("eventuous-sqlserver");
var sqlServerSnapshotsDb = sqlServer.AddDatabase("eventuous-sqlserver-snapshotsdb", "snapshots");

builder
    .AddProject<Projects.Banking_Api>("banking-api")
    .WithReference(kurrentdb, "kurrentdb")
    .WaitFor(kurrentdb)
    .WithReference(postgresSnapshotsDb, "postgresSnapshotsDb")
    .WaitFor(postgresSnapshotsDb)
    .WithReference(sqlServerSnapshotsDb, "sqlServerSnapshotsDb")
    .WaitFor(sqlServerSnapshotsDb);

builder
    .Build()
    .Run();
