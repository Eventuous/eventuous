using System.Text.Json;
using Eventuous.Sut.App;
using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Microsoft.AspNetCore.Http.Json;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;
using BookingService = Eventuous.Sut.AspNetCore.BookingService;

DefaultEventSerializer.SetDefaultSerializer(
    new DefaultEventSerializer(
        new JsonSerializerOptions(JsonSerializerDefaults.Web).ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
    )
);

var commandMap = new MessageMap()
    .Add<BookingApi.RegisterPaymentHttp, Commands.RecordPayment>(
        x => new Commands.RecordPayment(
            new BookingId(x.BookingId),
            x.PaymentId,
            new Money(x.Amount),
            x.PaidAt
        )
    );

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCommandService<BookingService, Booking>();
builder.Services.AddSingleton(commandMap);
builder.Services.AddControllers();

builder.Services.Configure<JsonOptions>(
    options => options.SerializerOptions.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb)
);

var app = builder.Build();

app.MapControllers();

app
    .MapAggregateCommands<Booking>()
    .MapCommand<BookRoom>((cmd, _) => cmd with { GuestId = TestData.GuestId });

app.Run();
