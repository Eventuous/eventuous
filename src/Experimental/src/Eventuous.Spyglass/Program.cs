using Eventuous.Spyglass.Modules;
using Eventuous.Sut.Domain;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

if (app.Environment.IsDevelopment()) {
    app.UseDeveloperExceptionPage();
}

BookingEvents.MapBookingEvents();

var scanner = new Scanner();
