# Eventuous ASP.NET Core

This package adds several DI extensions for `IServiceCollection`:

- `AddApplicationService` to register app services
- `AddAggregateStore` to register the `AggregateStore` and a given `IEventStore`
- `AddAggregate` to register aggregate types that require dependencies

Keep in mind that we don't recommend having dependencies in aggregates, so you'd normally not need to use `AddAggregate`.

When using `AddAggregate`, you should also call `builder.UseAggregateFactory()` in `Startup.Configure`.

You can also add Eventuous logs to the logging provider by calling `app.AddEventuousLogs()`
