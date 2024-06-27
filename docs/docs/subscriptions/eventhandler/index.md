---
title: "Event handlers"
description: "The last bit of the subscription process"
---

Event handlers are the final step in the subscription event processing [pipeline](../pipes). Each subscription has a single consumer that holds a collection of event handlers added to the subscription. The consumer calls all the event handlers simultaneously, collects the results, and then acknowledges the event to the subscription.

One common example of an event handler is a [read model](../../read-models) projector. Eventuous currently supports projecting events to MongoDB, but you can use any other database or file system.

## Abstractions

The default consumer holds classes that implement the basic interface of an event handler, defined as:

```csharp title="IEventHandler.cs"
public interface IEventHandler {
    string DiagnosticName { get; }
    
    ValueTask<EventHandlingStatus> HandleEvent(IMessageConsumeContext context);
}
```

The `DiagnosticName` property provides information that is used in log messages when the handler processes or fails to process the event. The `HandleEvent` function is called for each event received by the consumer and contains the actual event processing code. It should return a result of type `EventHandlingResult`.

The `BaseEventHandler` abstract class is commonly used as the base class for all event handlers, including custom ones, instead of implementing the interface directly. This class sets the DiagnosticName property to the type name of the event handler class.

Higher-level event handlers in Eventuous, such as `MongoProjection` and `GatewayHandler`, inherit from the `BaseEventHandler`.

## Handler results

A handler typically returns `Success` if the event was handled successfully, `Error` if the event handling failed, or `Ignored` if the handler has no code to process the event. The consumer determines the combined result based on the results returned by the handlers:

- Ignored events are considered processed successfully
- If all events are processed successfully, the consumer acknowledges the event
- If one or more handlers return an error, the consumer considers it an error and explicitly NACKs the event.
- 
The outcome of events that were not acknowledged by the consumer depends on the subscription type and its configuration.

## Custom handlers

If you need to implement a custom handler, such as a projector to a relational database, you typically use the `EventHandler` abstraction provided by Eventuous. This abstraction allows you to register typed handlers for specific event types in a map, and the HandleEvent function is already implemented in the interface, which will call the registered handler or return Ignored if no handler is registered for a given event type.

The `EventHandler` base class takes a [`TypeMapper`](../../persistence/serialisation.md#type-map) instance as a constructor argument. If a constructor argument is not provided, the default type mapper instance will be used. The `On<TEvent>` function uses the type mapper to check if the event type `TEvent` is registered in the type map, thus proactively causing the program to crash during startup if a handler is defined for an unregistered event type.

As an example, consider a simple handler that prints *$$$ MONEY! You got USD 100!* to the console when it receives the `PaymentRegistered` event, where the event's paid amount property is 100 and its currency is USD.

```csharp title="MoneyHandler.cs"
class MoneyHandler : EventHandler {
    public MoneyHandler(TypeMapper? typeMap = null) : base(typeMap) {
        On<PaymentRegistered>(
            async context => {
                await Console.Out.WriteLineAsync(
                    $"$$$ MONEY! You got {context.Message.Currency} {context.Message.AmountPaid}"
                );
            }
        );
    }
}
```

Another example would be a base class for a projector, which would use the handlers map and allow adding extended handlers for projecting events to a query model. Below is an example of a base class for a Postgres projector:

```csharp title="PostgresProjector.cs"
public abstract class PostgresProjector : EventHandler {
    readonly GetPostgresConnection _getConnection;

    protected PostgresProjector(
        GetPostgresConnection getConnection, 
        TypeMapper? mapper = null) : base(mapper) {
        _getConnection = getConnection;
    }

    protected void On<T>(ProjectToPostgres<T> handler) where T : class {
        base.On<T>(async ctx => await Handle(ctx).NoContext());

        async Task Handle(MessageConsumeContext<T> context) {
            await using var connection = _getConnection();
            await connection.OpenAsync(context.CancellationToken).ConfigureAwait(false);
            var cmd = await handler(connection, context).ConfigureAwait(false);
            await cmd.ExecuteNonQueryAsync(context.CancellationToken).ConfigureAwait(false);
        }
    }
}

public delegate Task<NpgsqlCommand> ProjectToPostgres<T>(
    NpgsqlConnection connection, 
    MessageConsumeContext<T> consumeContext)
    where T : class;
```

## Registering handlers

For an event handler to work, it needs to be added to a subscription. The `AddHandler` function on the subscription registration builder takes an instance of the `IEventHandler` interface as an argument. The `AddHandler` function is overloaded to accept a handler instance or a factory function that returns a handler instance.

You can find examples of adding handlers to subscriptions in the [subscription documentation](../sub-base/#registration).

Built-in projectors are event handlers, and they are added to the subscription in the same way as custom handlers.
