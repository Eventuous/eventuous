var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddAzureSqlServer("sql").RunAsContainer();
var db = sql.AddDatabase("database");

var serviceBus = builder.AddAzureServiceBus("sbemulators").RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue("PaymentsIntegration");

var blobs = builder.AddAzureStorage("storage").RunAsEmulator()
    .AddBlobs("blobs");

var bookings = builder.AddProject<Projects.Bookings>("bookings")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WithReference(serviceBus)
    .WithReference(blobs)
    .WaitFor(db)
    .WaitFor(serviceBus)
    .WaitFor(blobs);

var payments = builder.AddProject<Projects.Bookings_Payments>("payments")
    .WithExternalHttpEndpoints()
    .WithReference(db)
    .WithReference(serviceBus)
    .WithReference(blobs)
    .WaitFor(db)
    .WaitFor(serviceBus)
    .WaitFor(blobs);

builder.Build().Run();
