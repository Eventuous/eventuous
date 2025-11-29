var builder = DistributedApplication.CreateBuilder(args);

var db = builder.AddKurrentDB("kurrentdb", 2113)
    .WithEnvironment("EVENTSTORE_ENABLE_ATOM_PUB_OVER_HTTP", "true");

builder
    .AddProject<Projects.Banking_Api>("banking-api")
    .WithReference(db, "kurrentdb")
    .WaitFor(db);

builder
    .Build()
    .Run();
