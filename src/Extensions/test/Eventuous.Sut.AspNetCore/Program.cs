using Eventuous.Sut.AspNetCore;
using Eventuous.Sut.Domain;
using Eventuous.TestHelpers;
using Eventuous.Testing;
using Microsoft.AspNetCore.Http.Json;
using BookingService = Eventuous.Sut.AspNetCore.BookingService;

DefaultEventSerializer.SetDefaultSerializer(new DefaultEventSerializer(TestPrimitives.DefaultOptions));

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCommandService<BookingService, BookingState>();
builder.Services.AddEventStore<InMemoryEventStore>();
builder.Services.Configure<JsonOptions>(options => options.SerializerOptions.ConfigureForTests());

var app = builder.Build();

var config = app.Services.GetService<ConfigureWebApplication>();
config?.Invoke(app);

app.Run();

public partial class Program;
