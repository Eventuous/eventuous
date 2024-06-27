---
title: "Aggregate"
description: "Aggregate: consistency boundaries"
sidebar_position: 1
---

:::info
From version 0.14.0 using aggregates is **optional**. You can define your domain logic using [functional services](../application/func-service.md) instead.
:::

## Concept

:::note
If you are familiar with the concept, [scroll down](#implementation).
:::

`Aggregate` is probably the most important tactical pattern in Domain-Driven Design. It is a building block of the domain model. An `Aggregate` is a model on its own, a model of a particular business objects, which can be uniquely identified and by that distinguished from any other object of the same kind.

When handling a command, you need to ensure it only changes the state of a single aggregate. An aggregate boundary is a transaction boundary, so the state transition for the aggregate needs to happen entirely or not at all.

:::tip No entities
**TD;LR** Eventuous doesn't have entities other than the Aggregate Root. If you are okay with that, [scroll down](#implementation).
:::

Traditionally, DDD defines three concepts, which are related to aggregate:
- `Entity` - a representation of a business object, which has an identifier
- `Aggregate Root` - an entity, which might aggregate other entities and value objects
- `Aggregate` - the `Aggregate Root` and all the things inside it

The idea of an aggregate, which holds more than one entity, seems to be derived from the technical concerns of persisting the state. You can imagine an aggregate root type called `Booking` (for a hotel room), which holds a collection of `ExtraService` entities. Each of those entities represent a single extra service ordered by the guest when they made this booking. It could be a room service late at night, a baby cot, anything else that the guest needs to order in advance. Since those extra services might be also cancelled, we need to have a way to uniquely identify each of them inside the `Booking` aggregate, so those are entities.

If we decide to persist the `Booking` state in a relational database, the natural choice would be to have one table for `Booking` and one table for `ExtraService` with one-to-many relationship. Still, when loading the `Booking` state, we load the whole aggregate, so we have to read from the `Booking` table with inner join on the `ExtraService` table.

Those entities might also have behaviour, but to reach out to an entity within an aggregate, you go through the aggregate root (`Booking`). For example, to cancel the baby cot service, we'd have code like this:

```csharp
var booking = bookingRepository.Load(bookingId);
booking.CancelExtraService(extraServiceId);
bookingRepository.Save(booking);
```

In the `Booking` code it would expand to:

```csharp
void CancelExtraService(ExtraServiceId id) {
    extraServices.RemoveAll(x => x.Id == id);
    RecalculateTotal();
}
```

So, we have an entity here, but it doesn't really expose any behaviour. Even if it does, you first call the aggregate root logic, which finds the entity, and then routes the call to the entity.

In Eventuous, we consider it as a burden. If you need to find the entity in the aggregate root logic, why can't you also execute the operation logic right away? If you want to keep the entity logic separated, you can always create a module with a pure function, which takes the entity state and returns an event to the aggregate root.

The relational database persistence concern doesn't exist in Event Sourcing world. Therefore, we decided not to implement concepts like `Entity` and `AggregateRoot`. Instead, we provide a single abstraction for the logical and physical transaction boundary, which is the `Aggregate`.

## Implementation

Eventuous provides three abstract classes for the `Aggregate` pattern, which are all event-sourced. The reason to have three and not one is that all of them allow you to implement the pattern differently. You can choose the one you prefer.

### Aggregate

The `Aggregate` abstract class is quite technical and provides very little out of the box.

| Member            | Kind                 | What it's for                                                    |
|-------------------|----------------------|------------------------------------------------------------------|
| `Original`        | Read-only collection | Events that were loaded from the aggregate stream                |
| `Changes`         | Read-only collection | Events, which represent new state changes, get added here        |
| `Current`         | Read-only collection | The collection of the historical and new events                  |
| `ClearChanges`    | Method               | Clears the changes collection                                    |
| `OriginalVersion` | Property, `int`      | Original aggregate version at which it was loaded from the store |
| `Version`         | Property, `int`      | Current aggregate version after new events were applied          |
| `AddChange`       | Method               | Adds an event to the list of changes                             |

It also has two helpful methods, which aren't related to Event Sourcing:
- `EnsureExists` - throws if `Version` is `-1`
- `EnsureDoesntExist` - throws if `Version` is not `-1`

All other members are methods. You either need to implement them, or use one of the derived classes (see below).

| Member  | What it's for                                                                                                                                                                       |
|---------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Load`  | Given the list of previously stored events, restores the aggregate state. Normally, it's used for synchronous load, when all the stored events come from event store at once.       |
| `Fold`  | Applies a single state transition event to the aggregate state and increases the version. Normally, it's used for asynchronous loads, when events come from event store one by one. |

When building an application, you'd not need to use the `Aggregate` abstract class as-is. You still might want to use it to implement some advanced scenarios.

### Aggregate with state

Inherited from `Aggregate`, the `Aggregate<T>` adds a separate concept of the aggregate state. Traditionally, we consider state as part of the aggregate. However, state is the only part of the aggregate that mutated. The primary pattern in Eventuous is to separate state from the behaviour by splitting them into two distinct objects.

:::tip Event-sourced state
The `State` abstraction is described on the [State](state) page.
:::

The aggregate state in Eventuous is _immutable_. When applying an event to it, we get a new state.

The stateful aggregate class implements most of the abstract members of the original `Aggregate`. It exposes an API, which allows you to use the stateful aggregate base class directly.

| Member    | Kind     | What it's for                                                                                                                                                                                            |
|-----------|----------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| `Apply`   | Method   | Given a domain event, applies it to the state. Replaces the current state with the new version. Adds the event to the list of changes. Returns a tuple with the previous and the current state versions. |
| `State`   | Property | Returns the current aggregate state.                                                                                                                                                                     |
| `Current` | Property | Returns the collection of events loaded from the stream. If it's a new aggregate, it returns an empty list.                                                                                              |

Here's an example of a stateful aggregate:

```csharp title="Booking.cs"
public class Booking : Aggregate<BookingState> {
    public void BookRoom(string roomId, StayPeriod period, Money price, string? guestId = null) {
        EnsureDoesntExist();

        Apply(new RoomBooked(roomId, period.CheckIn, period.CheckOut, price.Amount, guestId));
    }

    public void Import(string roomId, StayPeriod period, Money price) {
        EnsureDoesntExist();

        Apply(new BookingImported(roomId, price.Amount, period.CheckIn, period.CheckOut));
    }

    public void RecordPayment(string paymentId, Money amount, DateTimeOffset paidAt) {
        EnsureExists();

        if (HasPaymentRecord(paymentId)) return;

        // The Apply function returns both previous and new state
        var (previousState, currentState) =
            Apply(new BookingPaymentRegistered(paymentId, amount.Amount));

        // Using the previous state can be useful for some scenarios
        if (previousState.AmountPaid != currentState.AmountPaid) {
            var outstandingAmount = currentState.Price - currentState.AmountPaid;
            Apply(new BookingOutstandingAmountChanged(outstandingAmount.Amount));

            if (outstandingAmount.Amount < 0) 
                Apply(new BookingOverpaid(-outstandingAmount.Amount));
        }

        // The next line only produces an event if the booking was not fully paid before
        if (!previousState.IsFullyPaid() && currentState.IsFullyPaid()) 
            Apply(new BookingFullyPaid(paidAt));
    }
    
    // This function uses the previously loaded events collection to 
    // check if the payment was already recorded. You can do the same using the state.
    public bool HasPaymentRecord(string paymentId)
        => Current.OfType<BookingPaymentRegistered>().Any(x => x.PaymentId == paymentId);

}
```

### Aggregate identity

Use the `AggregateId` abstract record, which needs a string value for its constructor:

```csharp title="BookingId.cs"
public record BookingId(string Value) : AggregateId(Value);
```

The abstract record overrides its `ToString` to return the string value as-is. It also has an implicit conversion operator, which allows you to use a string value without explicitly instantiating the identity record. However, we still recommend instantiating the identity explicitly to benefit from type safety.

The aggregate identity type is only used by the [command service](../application/app-service.md) and for calculating the [stream name](../persistence/aggregate-stream.md) for loading and saving events.

## Aggregate factory

Eventuous needs to instantiate your aggregates when it loads them from the store. New instances are also created by the `CommandService` when handling a command that operates on a new aggregate. Normally, aggregate classes don't have dependencies, so it is possible to instantiate one by calling its default constructor. However, you might need to have a dependency or two, like a domain service. We advise providing such dependencies when calling the aggregate function from the command service, as an argument. But it's still possible to instruct Eventuous how to construct aggregates that don't have a default parameterless constructor. That's the purpose of the `AggregateFactory` and `AggregateFactoryRegistry`.

The `AggregateFactory` is a simple function:

```csharp
public delegate T AggregateFactory<out T>() where T : Aggregate;
```

The registry allows you to add custom factory for a particular aggregate type. The registry itself is a singleton, accessible by `AggregateFactoryRegistry.Instance`. You can register your custom factory by using the `CreateAggregateUsing<T>` method of the registry:

```csharp title="Program.cs"
AggregateFactoryRegistry.CreateAggregateUsing(() => new Booking(availabilityService));
```

By default, when there's no custom factory registered in the registry for a particular aggregate type, Eventuous will create new aggregate instances by using reflections. It will only work when the aggregate class has a parameterless constructor (it's provided by the `Aggregate` base class).

It's not a requirement to use the default factory registry singleton. Both `CommandService` and `AggregateStore` have an optional parameter that allows you to provide the registry as a dependency. When not provided, the default instance will be used. If you use a custom registry, you can add it to the DI container as singleton.

### Dependency injection

The aggregate factory can inject registered dependencies to aggregates when constructing them. For this to work, you need to tell Eventuous that the aggregate needs to be constructed using the container. To do so, use the `AddAggregate<T>` service collection extension:

```csharp title="Program.cs"
builder.Services.AddAggregate<Booking>();
builder.Services.AddAggregate<Payment>(
    sp => new Payment(sp.GetRequiredService<PaymentProcessor>, otherService)
);
```

When that's done, you also need to tell the host to use the registered factories:

```csharp title="Program.cs"
app.UseAggregateFactory();
```

These extensions are available in the `Eventuous.AspNetCore` (DI extensions and `IApplicationBuilder` extensions) and `Eventuous.AspNetCore.Web` (`IHost` extensions).
