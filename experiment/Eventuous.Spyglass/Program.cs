using Eventuous.Spyglass;
using Eventuous.Spyglass.Modules;
using Eventuous.Tests.Model;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

BookingEvents.MapBookingEvents();

var scanner = new Scanner();

// Host.CreateDefaultBuilder(args)
//     .ConfigureWebHostDefaults(webBuilder => webBuilder.UseStartup<Startup>())
//     .Build()
//     .Run();
