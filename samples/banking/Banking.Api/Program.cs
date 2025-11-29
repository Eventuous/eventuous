using Banking.Api.Services;
using Banking.Domain.Accounts;
using Eventuous.KurrentDB;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddKurrentDBClient("kurrentdb");

builder.Services.AddEventStore<KurrentDBEventStore>();
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
