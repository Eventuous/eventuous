using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddAzureSqlServer("sql").RunAsContainer();
var bookingsDb = sql.AddDatabase("bookings-db");
var paymentsDb = sql.AddDatabase("payments-db");

var serviceBus = builder.AddAzureServiceBus("sbemulators").RunAsEmulator();
var queue = serviceBus.AddServiceBusQueue("PaymentsIntegration");

var blobs = builder.AddAzureStorage("storage").RunAsEmulator().AddBlobs("blobs");
var containers = blobs.AddBlobContainer("bookings-container");

var bookings = builder.AddProject<Projects.Bookings>("bookings")
    .WithHttpEndpoint()
    .WithReference(bookingsDb)
    .WithReference(serviceBus)
    .WithReference(blobs)
    .WaitFor(bookingsDb)
    .WaitFor(serviceBus)
    .WaitFor(blobs);

var payments = builder.AddProject<Projects.Bookings_Payments>("payments")
    .WithHttpEndpoint()
    .WithReference(paymentsDb)
    .WithReference(serviceBus)
    .WithReference(blobs)
    .WaitFor(paymentsDb)
    .WaitFor(serviceBus)
    .WaitFor(blobs);

var scalar = builder.AddScalarApiReference()
    .WithApiReference(bookings)
    .WithApiReference(payments)
    .WaitFor(bookings)
    .WaitFor(payments);

builder.Build().Run();
