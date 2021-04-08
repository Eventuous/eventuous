---
title: "Application service"
description: "Application service"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "application"
weight: 420
toc: true
---

{{% alert icon="👉" %}}
The Application Service base class is **optional**, it just makes your life a bit easier.
{{%/ alert %}}

## Concept

The command service itself performs the following operations, when handling one command:
1. Extract the aggregate id from the command, if necessary.
1. Instantiate all the necessary value objects. This could effectively reject the command, if value objects cannot be constructed. The command service could also load some other aggregates, or any other information, which is needed to execute the command, but won't change state.
1. If the command expects to operate on an existing aggregate instance, this instance gets loaded from the [Aggregate Store](../persistence/aggregate-store.md).
1. Execute an operation on the loaded (or new) aggregate, using values from the command, and the constructed value objects.
1. The aggregate either performs the operation and changes it state by producing new events, or rejects the operation.
1. If the operation was successful, the service persists new events to the store. Otherwise, it returns a failure to the edge.

## Application service base class

Eventuous provides a base class for you to build command services. It is a generic abstract class, which is typed to the aggregate type. You should create your own implementation of a command service for each aggregate type. As command execution is transactional, it can only operate on a single aggregate instance, and, logically, only one aggregate type.

### Registering command handlers

We have three methods, which you call in your class constructor to register the command handlers:

| Function | What's it for |
| -------- | ------------- |
| `OnNew` | Registers the handler, which expects no instance aggregate to exist (create, register, initialise, etc). It will get a new aggregate instance. The operation will fail when it will try storing the aggregate state due to version mismatch. |
| `OnExisting` | Registers the handler, which expect an aggregate instance to exist. You need to provide a function to extract the aggregate id from the command. The handler will get the aggregate instance loaded from the store, and will throw if there's no aggregate to load. |
| `OnAny` | Used for handlers, which can operate both on new and existing aggregate instances. The command service will _try_ to load the aggregate, but won't throw if the load fails, and will pass a new instance instead. |

Here is an example of a command service form our test project:

```csharp
public class BookingService
  : ApplicationService<Booking, BookingState, BookingId> {
    public BookingService(IAggregateStore store) : base(store) {
        OnNew<Commands.BookRoom>(
            (booking, cmd)
                => booking.BookRoom(
                    new BookingId(cmd.BookingId),
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut),
                    cmd.Price,
                    cmd.BookedBy,
                    cmd.BookedAt
                )
        );

        OnAny<Commands.ImportBooking>(
            cmd => new BookingId(cmd.BookingId),
            (booking, cmd)
                => booking.Import(
                    new BookingId(cmd.BookingId),
                    cmd.RoomId,
                    new StayPeriod(cmd.CheckIn, cmd.CheckOut)
                )
        );
    }
}
```

You pass the command handler as a function to one of those methods. The function can be inline, like in the example, or it could be a method in the command service class.

In addition, `OnAny` and `OnExisting` need a function, which extracts the aggregate id from the command, as both of those methods will try loading the aggregate instance from the store.

### Calling the service from the API

From your API you can use the command service as a dependency. It doesn't need to be a transient dependency as it is stateless. When using a DI container, the command service can be registered as a singleton. You don't need any interfaces for it.

In the API (controller, gRPC service or message consumer), call the command service directly with the data you got from the API contract. For example:

```csharp
[Route("api/booking")]
[ApiController]
public class BookingsCommandApi : ControllerBase {
    readonly BookingsCommandService _service;
    readonly GetNow                 _getNow;

    public BookingsCommandApi(
        BookingsCommandService service,
        GetNow getNow
    ) {
        _service = service;
        _getNow  = getNow;
    }

    [HttpPost]
    [Authorize]
    public Task AddBooking(AddBooking addBooking) {
        var cmd =
            new BookingCommands.AddBooking(
                addBooking.BookingId,
                addBooking.RoomId,
                addBooking.CheckInDate,
                addBooking.CheckOutDate,
                addBooking.Price,
                User.GetUserId(),
                _getNow()
            );
        return _service.Handle(cmd);
    }
}
```

As you can see, the API endpoint doesn't contain much of a logic. However, you can still include some easy checks like mandatory field validations, or ensuring that emails or phone number are indeed in the right format. The latter, however, could also be done when you construct value objects in the command service.

When you instantiate a command, you just need to call the `Handle` function of the command service.

### Result

The command service will return an instance of `Result`.

It could be an `OkResult`, which contains the new aggregate state and the list of new events. You use the data in the result to pass it over to the caller, if needed.

If the operation was not successful, the command service will throw an exception. We plan to change this behaviour later in favour of producing a failure result.

### Bootstrap

If you registered the `EsdbEventStore` and the `AggregateStore` in your `Startup` as described on the [Aggregate store]({{< ref "aggregate-store" >}}) page, you can also register the application service:

```csharp
services.AddSingleton<BookingCommandService>();
```

When also using `AddControllers`, you get the command service injected to your controllers.
