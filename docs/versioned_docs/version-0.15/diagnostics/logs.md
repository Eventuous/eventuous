# Logging

Eventuous uses the ASP.NET Core logging with `ILoggerFactory` and `ILogger<T>`, so you can use the standard logging facilities to log diagnostics. For internal logging, Eventuous uses multiple [event sources](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/eventsource), which you can collect and analyse with tools like `dotnet-trace`.

It's also possible to expose internal Eventuous logs using the `UseEventuousLogs` extension for `IHost`, which is available as part of `Eventuous.Extensions.DependencyInjection` package.
