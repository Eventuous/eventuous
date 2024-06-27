---
title: "Command API"
description: "Auto-generated HTTP API for command handling"
sidebar_position: 3
---

## Controller base

When using a command service from an HTTP controller, you'd usually inject the service as a dependency, and call it's `Handle` method using the request body:

```csharp title="Api/BookingCommandApi.cs"
[Route("/booking")]
public class CommandApi : ControllerBase {
    ICommandService<Booking> _service;

    public CommandApi(ICommandService<Booking> service) => _service = service;

    [HttpPost]
    [Route("book")]
    public async Task<ActionResult<Result>> BookRoom(
        [FromBody] BookRoom cmd, 
        CancellationToken cancellationToken
    ) {
        var result = await _service.Handle(cmd, cancellationToken);
        result Ok(result);
    }
}
```

The issue here is there's no way to know if the command was successful or not. As the command service won't throw an exception if the command fails, we can't return an error via the HTTP response, unless we parse the [result](app-service.md#result) and return a meaningful HTTP response.

Eventuous allows you to simplify the command handling in the API controller by providing a `CommandHttpApiBase<TAggregate>` abstract class, which implements the `ControllerBase` and contains the `Handle` method. The class takes `ICommandService<TAggregate>` as a dependency. The `Handle` method will call the command service, and also convert the handling result to `ActionResult<Result>`. Here are the rules for exception handling:

| Result exception                 | HTTP response |
|----------------------------------|---------------|
| `OptimisticConcurrencyException` | `Conflict`    |
| `AggregateNotFoundException`     | `NotFound`    |
| Any other exception              | `BadRequest`  |

Here is an example of a command API controller:

```csharp
[Route("/booking")]
public class CommandApi : CommandHttpApiBase<Booking> {
    public CommandApi(ICommandService<Booking> service) : base(service) { }

    [HttpPost]
    [Route("book")]
    public Task<ActionResult<Result>> BookRoom(
        [FromBody] BookRoom cmd, 
        CancellationToken cancellationToken
    ) => Handle(cmd, cancellationToken);
}
```

We recommend using the `CommandHttpApiBase` class when you want to handle commands using the HTTP API.

When using [functional services](./func-service.md) you can use the `CommandHttpApiBaseFunc` base class, which works exactly the same way:

```csharp
[Route("/booking")]
public class CommandApi : CommandHttpApiBaseFunc<Booking> {
    public CommandApi(IFuncCommandService<Booking> service) : base(service) { }

    [HttpPost]
    [Route("book")]
    public Task<ActionResult<Result>> BookRoom(
        [FromBody] BookRoom cmd, 
        CancellationToken cancellationToken
    ) => Handle(cmd, cancellationToken);
}
```

## Generated command API

Eventuous can use your command service to generate a command API. Such an API will accept JSON models matching the application service command contracts, and pass those commands as-is to the application service. This feature removes the need to create API endpoints manually using controllers or .NET minimal API. 

To use generated APIs, you need to add `Eventuous.AspNetCore.Web` package.

All the auto-generated API endpoints will use the `POST` HTTP method.

### Annotating commands

For Eventuous to understand what commands need to be exposed as API endpoints and on what routes, those commands need to be annotated by the `HttpCommand` attribute:

```csharp
[HttpCommand<Booking>(Route = "payment")]
public record ProcessPayment(string BookingId, float PaidAmount);
```

You can skip the `Route` property, in that case Eventuous will use the command class name. For the example above the generated route would be `processPayment`. We recommend specifying the route explicitly as you might refactor the command class and give it a different name, and it will break your API if the route is auto-generated.

If your application has a single command service working with a single aggregate type, you don't need to specify the aggregate type, and then use a different command registration method (described below).

Another way to specify the aggregate type for a group of commands is to annotate the parent class (command container):

```csharp
[AggregateCommands<Booking>()]
public static class BookingCommands {
    [HttpCommand(Route = "payment")]
    public record ProcessPayment(string BookingId, float PaidAmount);
}
```

In such case, Eventuous will treat all the commands defined inside the `BookingCommands` static class as commands operating on the `Booking` aggregate.

Also, you don't need to specify the aggregate type in the command annotation if you use the `MapAggregateCommands` registration (see below).

Finally, you don't need to annotate the command at all if you use the explicit command registration with the route parameter.

### Registering commands

There are several extensions for `IEndpointRouteBuilder` that allow you to register HTTP endpoints for one or more commands.

#### Single command

The simplest way to register a single command is to make it explicitly in the bootstrap code:

```csharp
var builder = WebApplication.CreateBuilder();

// Register the app service
builder.Services.AddCommandService<BookingService, Booking>();

var app = builder.Build();

// Map the command to an API endpoint
app.MapCommand<ProcessPayment, Booking>("payment");

app.Run();

record ProcessPayment(string BookingId, float PaidAmount);
```

If you annotate the command with the `HttpCommand` attribute, and specify the route, you can avoid providing the route when registering the command:

```csharp
app.MapCommand<BookingCommand, Booking>();
...

[HttpCommand(Route = "payment")]
public record ProcessPayment(string BookingId, float PaidAmount);
```

#### Multiple commands for an aggregate

You can also register multiple commands for the same aggregate type, without a need to provide the aggregate type in the command annotation. To do that, use the extension that will create an `CommandServiceRouteBuilder`, then register commands using that builder:

```csharp
app
    .MapAggregateCommands<Booking>()
    .MapCommand<ProcessPayment>()
    .MapCommand<ApplyDiscount>("discount");
    
...

// route specified in the annotation
[HttpCommand(Route = "payment")] 
public record ProcessPayment(string BookingId, float PaidAmount);

// No annotation needed
public record ApplyDiscount(string BookingId, float Discount);
```

#### Discover commands

There are two extensions that are able to scan your application for annotated commands, and register them automatically.

First, the `MapDiscoveredCommand<TAggregate>`, which assumes your application only serves commands for a single aggregate type:

```csharp
app.MapDiscoveredCommands<Booking>();

...
[HttpCommand(Route = "payment")] 
record ProcessPayment(string BookingId, float PaidAmount);
```

For it to work, all the commands must be annotated and have the route defined in the annotation.

The second extension will discover all the annotated commands, which need to have an association with the aggregate type by using the `Aggregate` argument of the attribute, or by using the `AggregateCommands` attribute on the container class (described above):

```csharp
app.MapDiscoveredCommands();

...

[HttpCommand<Booking>(Route = "bookings/payment")] 
record ProcessPayment(string BookingId, float PaidAmount);

[AggregateCommands<Payment>]
class V1.PaymentCommands {
    [HttpCommand(Route = "payments/register")]
    public record RegisterPayment(string PaymentId, string Provider, float Amount);
    
    [HttpCommand(Route = "payments/refund")]
    public record RefundPayment(string PaymentId);
}
```

Both extensions will scan the current assembly by default, but you can also provide a list of assemblies to scan as an argument:

```csharp
app.MapDiscoveredCommands(typeof(V1.PaymentCommands).Assembly);
```

### Using HttpContext data

Commands processed by the command service might include properties that aren't provided by the API client, but are available in the `HttpContext` object. For example, you can think about the user that is making the request. The details about the user, and the user claims, are available in `HttpContext`.

You can instruct Eventuous to enrich the command before it gets sent to the command service, using the `HttpContext` data. In that case, you also might want to hide the command property from being exposed to the client in the OpenAPI spec.

To hide a property from being exposed to the client, use the `JsonIgnore` attribute:

```csharp
[HttpCommand(Route = "book")]
public record BookRoom(string RoomId, string BookingId, [property: JsonIgnore] string UserId);
```

Then, you can use the `HttpContext` data in your command:

```csharp
app
    .MapAggregateCommands<Booking>()
    .MapCommand<BookRoom>((cmd, ctx) => cmd with { UserId = ctx.User.Identity.Name });
```

When the command is mapped to the API endpoint like that, and the property is ignored, the OpenAPI specification won't include the ignored property, and the command service will get the command populated with the user id from `HttpContext`.
