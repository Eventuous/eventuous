using Banking.Api.Services;
using Banking.Domain.Accounts;
using Eventuous.KurrentDB;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

//----------------------------------------------------------------
// Snapshot store registration

var postgresSnapshotDbConnectionString = builder.Configuration.GetConnectionString("postgresSnapshotsDb");
if (postgresSnapshotDbConnectionString == null) {
    throw new InvalidOperationException("Postgres snapshots db connection string should be not null");
}

//builder.Services.AddPostgresSnapshotStore(postgresSnapshotDbConnectionString, initializeDatabase: true);

var sqlServerSnapshotsDbConnectionString = builder.Configuration.GetConnectionString("sqlServerSnapshotsDb");
if (sqlServerSnapshotsDbConnectionString == null) {
    throw new InvalidOperationException("SqlServer snapshots db connection string should be not null");
}

//builder.Services.AddSqlServerSnapshotStore(sqlServerSnapshotsDbConnectionString, initializeDatabase: true);

var mongoDbSnapshotsDbConnectionString = builder.Configuration.GetConnectionString("mongoDbSnapshotsDb");
if (mongoDbSnapshotsDbConnectionString == null) {
    throw new InvalidOperationException("Mongodb snapshots db connection string should be not null");
}

//builder.Services.AddMongoSnapshotStore(mongoDbSnapshotsDbConnectionString, initializeIndexes: true);

var redisConnectionString = builder.Configuration.GetConnectionString("redis");
if (redisConnectionString == null) {
    throw new InvalidOperationException("Redis connection string should be not null");
}

builder.Services.AddRedisSnapshotStore(redisConnectionString, database: 0);

//----------------------------------------------------------------
// Event store registration

builder.AddKurrentDBClient("kurrentdb");
builder.Services.AddEventStore<KurrentDBEventStore>();

//----------------------------------------------------------------

builder.Services.AddCommandService<AccountService, AccountState>();

var app = builder.Build();

app.MapGet("/accounts/{id}/deposit/{amount}", async ([FromRoute] string id, [FromRoute] decimal amount, [FromServices] AccountService accountService) => {
    var cmd = new AccountService.Deposit(id, amount);
    var res = await accountService.Handle(cmd, default);

    return res.Match<object>(ok => ok, err => err);
});

app.MapGet("/accounts/{id}/withdraw/{amount}", async ([FromRoute] string id, [FromRoute] decimal amount, [FromServices] AccountService accountService) => {
    var cmd = new AccountService.Withdraw(id, amount);
    var res = await accountService.Handle(cmd, default);

    return res.Match<object>(ok => ok, err => err);
});

app.Run();
