using System.Runtime.InteropServices;
using Scalar.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// Same image as the repository's KurrentDB test fixtures
var kurrentImage = RuntimeInformation.ProcessArchitecture == Architecture.Arm64
    ? "kurrentplatform/kurrentdb:26.1.1-experimental-arm64-10.0-noble"
    : "kurrentplatform/kurrentdb:26.1.1";
var imageParts = kurrentImage.Split(':');

var kurrentdb = builder.AddContainer("kurrentdb", imageParts[0], imageParts[1])
    .WithArgs("--insecure", "--run-projections=All", "--enable-atom-pub-over-http")
    .WithHttpEndpoint(port: 2113, targetPort: 2113, name: "http");

var kurrentdbEndpoint = kurrentdb.GetEndpoint("http");

var mongoUser     = builder.AddParameter("mongo-user", "mongoadmin");
var mongoPassword = builder.AddParameter("mongo-password", "secret", secret: true);

var mongo = builder.AddMongoDB("mongo", userName: mongoUser, password: mongoPassword)
    // MongoDB 8.3 refuses to start on Linux kernel 6.19+ (SERVER-121912)
    .WithImageTag("7.0");

var storage = builder.AddAzureStorage("storage").RunAsEmulator();
var blobs   = storage.AddBlobs("blobs");
storage.AddBlobContainer("bookings-container", blobContainerName: "bookings");

var bookings = builder.AddProject<Projects.Bookings>("bookings")
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health")
    .WithReference(blobs)
    .WithEnvironment(ctx => {
            ctx.EnvironmentVariables["KurrentDB__ConnectionString"] = ReferenceExpression.Create(
                $"kurrentdb://{kurrentdbEndpoint.Property(EndpointProperty.Host)}:{kurrentdbEndpoint.Property(EndpointProperty.Port)}?tls=false"
            );
            ctx.EnvironmentVariables["Mongo__ConnectionString"] = mongo.Resource.ConnectionStringExpression;
        }
    )
    .WaitFor(kurrentdb)
    .WaitFor(mongo)
    .WaitFor(blobs);

var payments = builder.AddProject<Projects.Bookings_Payments>("payments")
    .WithHttpEndpoint()
    .WithHttpHealthCheck("/health")
    .WithEnvironment(ctx => {
            ctx.EnvironmentVariables["KurrentDB__ConnectionString"] = ReferenceExpression.Create(
                $"kurrentdb://{kurrentdbEndpoint.Property(EndpointProperty.Host)}:{kurrentdbEndpoint.Property(EndpointProperty.Port)}?tls=false"
            );
            ctx.EnvironmentVariables["Mongo__ConnectionString"] = mongo.Resource.ConnectionStringExpression;
        }
    )
    .WaitFor(kurrentdb)
    .WaitFor(mongo);

builder.AddScalarApiReference()
    .WithApiReference(bookings)
    .WithApiReference(payments)
    .WaitFor(bookings)
    .WaitFor(payments);

builder.Build().Run();
