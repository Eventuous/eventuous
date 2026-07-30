using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.Testing;
using Microsoft.AspNetCore.Http.Json;
using BookingService = Eventuous.Sut.AspNetCore.BookingService;

EventSerializer.SetDefault(new DefaultEventSerializer(TestPrimitives.DefaultOptions));

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCommandService<BookingService, BookingState>();
builder.Services.AddEventStore<InMemoryEventStore>();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.ConfigureForTests());

var app = builder.Build();

var config = app.Services.GetService<ConfigureWebApplication>();
config?.Invoke(app);

app.Run();

#pragma warning disable ASP0027
public partial class Program;
#pragma warning restore ASP0027
