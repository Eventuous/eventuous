---
title: "Using services in HTTP API"
description: "Auto-generated HTTP API for command handling"
sidebar_position: 3
---

:::note
Install `Eventuous.Extensions.AspNetCore` package for using Eventuous HTTP API support.
:::

## Controller base

Eventuous allows you to simplify the command handling in the API controller by using a `CommandHttpApiBase<TState>` abstract class, which implements
the `ControllerBase` and contains the `Handle` method. The class takes `ICommandService<TState>` as a dependency. The `Handle` method will call the command
service, and also convert the handling result to `ActionResult<Result<TState>.Ok>`. Here are the rules for exception handling:

| Result exception                 | HTTP response |
|----------------------------------|---------------|
| `OptimisticConcurrencyException` | `Conflict`    |
| `AggregateNotFoundException`     | `NotFound`    |
| Any other exception              | `BadRequest`  |

Here is an example of a command API controller:

```csharp
[Route("/booking")]
public class CommandApi(ICommandService<BookingState> service) 
    : CommandHttpApiBase<BookingState> {
    [HttpPost]
    [Route("book")]
    public Task<ActionResult<Result<BookingState>.Ok>> BookRoom(
        [FromBody] BookRoom cmd, 
        CancellationToken cancellationToken
    ) => Handle(cmd, cancellationToken);
}
```

Although the controller endpoint method returns `ActionResult<Result<TState>.Ok>`, it does that only if the command was handled successfully. If the command
service was unable to process the command, it will generate an instance of:

* `ProblemDetails` with status code `404` if there's no stream to operate on
* `ProblemDetails` with status code `409` in case of optimistic concurrency issues
* `ValidationProblemDetails` with `400` status code in case there was a domain exception
* `ProblemDetails` with status code `500` for any other issue

For making those responses available in the OpenAPI generated documentation, you'd need to annotate your controller methods accordingly. Eventuous provides
several attributes to help with that, as well as an attribute to specify the success return type if your controller endpoint returns `IActionResult`:

```csharp
public class BookingApi(ICommandService<BookingState> service)
    : CommandHttpApiBase<BookingState>(service) {
    [HttpPost("v2/pay")]
    [ProducesResult<BookingState>]
    [ProducesConflict]
    [ProducesDomainError]
    [ProducesNotFound]
    public async Task<IActionResult?> RegisterPayment(
        [FromBody] RegisterPaymentHttp cmd, 
        CancellationToken cancellationToken
    ) {
        var result = await Handle(cmd, cancellationToken);

        return result.Result;
    }
}
```

## Generated command API

Eventuous can use your command service to generate a command API. Such an API will accept JSON models matching the application service command contracts, and
pass those commands as-is to the application service. This feature removes the need to create API endpoints manually using controllers or .NET minimal API.

All the auto-generated API endpoints will use the `POST` HTTP method.

### Annotating commands

For Eventuous to understand what commands need to be exposed as API endpoints and on what routes, those commands need to be annotated by the `HttpCommand`
attribute:

```csharp
[HttpCommand<BookingState>(Route = "payment")]
public record ProcessPayment(string BookingId, float PaidAmount);
```

Eventuous will then map the command to an HTTP `POST` endpoint that will resolve an instance of `ICommandService<BookingState>` which can be either a
aggregate-based command service or a functional service, and pass the command to it.

You can skip the `Route` property, in that case Eventuous will use the command class name. For the example above the generated route would be `processPayment`.
We recommend specifying the route explicitly as you might refactor the command class and give it a different name, and it will break your API if the route is
auto-generated.

If your application has a single command service working with a single state type, you don't need to specify the state type, and then use a different command
registration method (described below).

Another way to specify the state type for a group of commands is to annotate the parent class (command container):

```csharp
[StateCommands<BookingState>()]
public static class BookingCommands {
    [HttpCommand(Route = "payment")]
    public record ProcessPayment(string BookingId, float PaidAmount);
}
```

In such case, Eventuous will treat all the commands defined inside the `BookingCommands` static class as commands operating on `BookingState`.

Also, you don't need to specify the state type in the command annotation if you use the `MapCommands` registration (see below).

Finally, you don't need to annotate the command at all if you use the explicit command registration with the route parameter.

### Registering commands

There are several extensions for `IEndpointRouteBuilder` that allow you to register HTTP endpoints for one or more commands.

#### Single command

The simplest way to register a single command is to make it explicitly in the bootstrap code:

```csharp
var builder = WebApplication.CreateBuilder();

// Register the app service
builder.Services.AddCommandService<BookingService, BookingState>();

var app = builder.Build();

// Map the command to an API endpoint
app.MapCommand<ProcessPayment, BookingState>("payment");

app.Run();

record ProcessPayment(string BookingId, float PaidAmount);
```

If you annotate the command with the `HttpCommand` attribute, and specify the route, you can avoid providing the route when registering the command:

```csharp
app.MapCommand<ProcessPayment, BookingState>();
...

[HttpCommand(Route = "payment")]
public record ProcessPayment(string BookingId, float PaidAmount);
```

#### Multiple commands for one service

You can also register multiple commands for the same service type without a need to provide the state type in the command annotation. To do that, use the
extension that will create an `CommandServiceRouteBuilder`, then register commands using that builder:

```csharp
app
    .MapCommands<BookingState>()
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

First, the `MapDiscoveredCommand<TState>`, which assumes your application only serves commands with a single service type:

```csharp
app.MapDiscoveredCommands<BookingState>();

...
[HttpCommand(Route = "payment")] 
record ProcessPayment(string BookingId, float PaidAmount);
```

For it to work, all the commands must be annotated and have the route defined in the annotation.

The second extension will discover all the annotated commands, which need to have an association with the command service type by using the `StateType` argument
of the attribute, or by using the `HttpCommands` attribute on the container class (described above):

```csharp
app.MapDiscoveredCommands();

...

[HttpCommand<BookingState>(Route = "bookings/payment")] 
record ProcessPayment(string BookingId, float PaidAmount);

[HttpCommands<PaymentState>]
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

## Command separation and enrichment

Eventuous supports converting external (public) commands to internal (private) commands, as well as enriching commands with data that isn't coming directly via
the message (as, HTTP or gRPC message) but is available in the transport context (`HttpContext` as an example).

Here are some example cases for this:

* Convert external HTTP commands to internal domain commands. It allows using value objects for internal commands and support early validation of the values
  provided in the HTTP call, and avoid unnecessary reads from the database if the request is invalid.
* Convert commands in multiple API versions to the same domain commands.
* Populating command properties with values available in the transport context, like `HttpContext.User`.

### For controllers

When using the `CommandHttpApiBase` for controllers, you can provide an optional command map that instructs the controller how the HTTP API contract can be
enriched with `HttpContext` data or converted to a different command type. You'd need to add all command transformations and enrichments to the command map, so
it can be used across all the controllers.

When you want to separate internal and external commands, commands could be defined like this:

```csharp 
// HTTP contract
public record RegisterPaymentHttp(
    string         BookingId,
    string         PaymentId,
    float          Amount,
    DateTimeOffset PaidAt
);

// Domain command using value objects
public record RecordPayment(
    BookingId      BookingId,
    string         PaymentId,
    Money          Amount,
    DateTimeOffset PaidAt,
    string         PaidBy // Additional information fro HttpContext
);
```

For example, the code below creates an instance of a command map and adds one transformation to it:

```csharp
var commandMap = new CommandMap<HttpContext>()
    .Add<RegisterPaymentHttp, RecordPayment>((x, ctx) => 
        new(
            new BookingId(x.BookingId), 
            x.PaymentId, 
            new Money(x.Amount), 
            x.PaidAt, 
            ctx.User.Identity?.Name
        )
    );
```

Of course, if you don't want to separate external and internal commands, you can use the same but simplified technique. In that case, you'd want to hide the
additional property using the `JsonIgnore` attribute, so it doesn't show up in the OpenAPI spec.

```csharp
public record RegisterPayment(BookingId Id, string PaymentId, Money Amount, LocalDate PaidAt) {
    [JsonIgnore] 
    string? PaidBy {get; init; }
}

var commandMap = new CommandMap<HttpContext>()
    .Add<RecordPayment>((x, ctx) => x with { PaidBy = ctx.User.Identity?.Name });
```

For using the command map in controllers, you'd need to register it in the DI container as a singleton:

```csharp title="Program.cs"
builder.Services.AddSingleton(commandMap);
```

### For generated routes

It's possible to assign command properties from `HttpContext` when using extended versions of the `MapCommand` function. This method doesn't work with
discovered commands.

First, you'd want to hide properties that are populated from `HttpContext` so they don't show up in the OpenAPI spec. To hide a property from being exposed to
the client, use the `JsonIgnore` attribute:

```csharp
[HttpCommand(Route = "book")]
public record BookRoom(string RoomId, string BookingId, [property: JsonIgnore] string UserId);
```

Then, you can use the `HttpContext` data in your command:

```csharp title="Program.cs"
var app = builder.Build();

app
    .MapCommands<BookingState>()
    .MapCommand<BookRoom>((cmd, ctx) => cmd with { UserId = ctx.User.Identity.Name });
```

When the command is mapped to the API endpoint like that, and the property is ignored, the OpenAPI specification won't include the ignored property, and the
command service will get the command populated with the user id from `HttpContext`.

Uou can also use the `Map` method to map the contract to the domain command. You can also use data from the `HttpContext` to add additional information to the
command, like the user identity. There's no need to add ignored properties to the contract in this case.

Consider the contract record being decorated by the `HttpCommand` attribute:

```csharp title="HTTP contract"
[HttpCommand(Route = "pay")]
public record RegisterPaymentHttp(
    string         BookingId,
    string         PaymentId,
    float          Amount,
    DateTimeOffset PaidAt
);
```

We can then define the domain command with an additional `PaidBy` property:

```csharp title="Domain command"
public record RecordPayment(
    BookingId      BookingId,
    string         PaymentId,
    Money          Amount,
    DateTimeOffset PaidAt,
    string         PaidBy // Additional information fro HttpContext
);
```

Then, use a more advanced overload of `MapCommand` to apply transformation and enrichment:

```csharp title="Program.cs"
var app = builder.Build();

app
    .MapCommands<BookingState>()
    .MapCommand<ProcessPaymentHttp, Commands.ProcessPayment>(
        (cmd, ctx) => new Commands.ProcessPayment(
            new BookingId(cmd.BookingId), // Create value object from primitive
            cmd.PaymentId,                // Use primitive
            new Money(cmd.Amount),
            cmd.PaidAt,
            ctx.User.Identity.Name        // Use HttpContext to get user details
        )
    );
```
