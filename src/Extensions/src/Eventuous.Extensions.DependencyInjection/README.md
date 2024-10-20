# Eventuous ASP.NET Core

This package adds several DI extensions for `IServiceCollection`:

- `AddCommandService` to register app services
- `AddAggregateStore` to register the `AggregateStore` and a given `IEventStore` (Eventuous does not need aggregate store to be registered, only use it if you use the aggregate store in your application directly)
- `AddAggregate` to register aggregate types that require dependencies

Keep in mind that we don't recommend having dependencies in aggregates, so you'd normally not need to use `AddAggregate`.
