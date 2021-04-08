---
title: "Aggregate"
description: "Aggregate"
date: 2020-11-12T13:26:54+01:00
lastmod: 2020-11-12T13:26:54+01:00
draft: false
images: []
menu:
  docs:
    parent: "domain"
weight: 200
toc: true
---

## Concept

{{% alert icon="👉" color="light" %}}
If you are familiar with the concept, [scroll down](#implementation).
{{% /alert %}}

`Aggregate` is probably the most important tactical pattern in Domain-Driven Design. It is a building block of the domain model. An `Aggregate` is a model on its own, a model of a particular business objects, which can be uniquely identified and by that distinguished from any other object of the same kind.

When handling a command, you need to ensure it only changes the state of a single aggregate. An aggregate boundary is a transaction boundary, so the state transition for the aggregate needs to happen entirely or not at all.

{{% alert icon="👉" color="light" %}}
**TD;LR** Eventuous doesn't have entities other than the Aggregate Root. If you are okay with that, [scroll down](#implementation).
{{% /alert %}}

Traditionally, DDD defines three concepts, which are related to aggregate:
- `Entity` - a representation of a business object, which has an identifier
- `Aggregate Root` - an entity, which might aggregate other entities and value objects
- `Aggregate` - the `Aggregate Root` and all the things inside it

The idea of an aggregate, which holds more than one entity, seems to be derived from the technical concerns of persisting the state. You can imagine an aggregate root type called `Booking` (for a hotel room), which holds a collection of `ExtraService` entities. Each of those entities represent a single extra service ordered by the guest when they made this booking. It could be a room service late at night, a baby cot, anything else that the guest needs to order in advance. Since those extra services might be also cancelled, we need to have a way to uniquely identify each of them inside the `Booking` aggregate, so those are entities.

If we decide to persist the `Booking` state in a relational database, the natural choice would be to have one table for `Booking` and one table for `ExtraService` with one-to-many relationship. Still, when loading the `Booking` state, we load the whole aggregate, so we have to read from the `Booking` table with inner join on the `ExtraService` table.

Those entities might also have behaviour, but to reach out to an entity within an aggregate, you go through the aggregate root (`Booking`). For example, to cancel the baby cot service, we'd have code like this:

```csharp
var booking = bookingRepository.Load(bookingId);
booling.CancelExtraService(extraServiceId);
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

| Member | Kind | What it's for |
| ------ | ---- | ------------- |
| `Changes` | Read-only collection | Events, which represent new state changes, get added here |
| `ClearChanges` | Method | Clears the changes collection |
| `Version` | Property, `int` | Current aggregate version, used for optimistic concurrency. Default is `-1` |
| `AddChange` | Method | Adds an event to the list of changes |

It also has two helpful methods, which aren't related to Event Sourcing:
- `EnsureExists` - throws if `Version` is `-1`
- `EnsureDoesntExist` - throws if `Version` is not `-1`

All other members are methods. You either need to implement them, or use one of the derived classes (see below).

| Member | What it's for |
| ------ | ------------- |
| `Load` | Given the list of previously stored events, restores the aggregate state. Normally, it's used for synchronous load, when all the stored events come from event store at once. |
| `Fold` | Applies a single state transition event to the aggregate state and increases the version. Normally, it's used for asynchronous loads, when events come from event store one by one. |
| `GetId` | Returns the aggregate identity as `string`. As most databases support string identity, it's the most generic type to support persistence. |

When building an application, you'd not need to use the `Aggregate` abstract class as-is. You still might want to use it to implement some advanced scenarios.

### Aggregate with state

Inherited from `Aggregate`, the `Aggregate<T>` adds a separate concept of the aggregate state. Traditionally, we consider state as part of the aggregate. However, state is the only part of the aggregate that mutated. We decided to separate state from the behaviour by splitting them into two distinct objects.

The aggregate state in Eventuous is _immutable_. When applying an event to it, we get a new state.

The stateful aggregate class implements most of the abstract members of the original `Aggregate`. It exposes an API, which allows you to use the stateful aggregate base class directly.

| Member | Kind | What it's for |
| ------ | ---- | ------------- |
| `Apply` | Method | Given a domain event, applies it to the state. Replaces the current state with the new version. Adds the event to the list of changes. |
| `State` | Property | Returns the current aggregate state. |

As we don't know how to extract the aggregate identity from this implementation, you still need to implement the `GetId` function.

#### Aggregate state

We have an abstraction for the aggregate state. It might seem unnecessary, but it has a single abstract method, which you need to implement for your own state classes. As mentioned previously, we separated the aggregate behaviour from its state. Moving along, we consider event-based state transitions as part of the state handling. Therefore, the state objects needs to expose an API to receive events and produce a new instance of itself (remember that the state is immutable).

To support state immutability, `AggregateState` is an abstract _record_, not class. Therefore, it supports immutability out of the box and supports `with` syntax to make state transitions easier.

A record, which inherits from `AggregateState` needs to implement a single function called `When`. It gets an event as an argument and returns the new state instance. For example:

```csharp
record BookingState : AggregateState<BookingState, BookingId> {
    decimal Price { get; init; }

    public override BookingState When(object @event)
        => @event switch {
            RoomBooked booked        => this with
                { Id = new BookingId(booked.BookingId), Price = booked.Price },
            BookingImported imported => this with
                { Id = new BookingId(imported.BookingId) },
            _                        => this
        };
}
```

The default branch of the switch expression returns the current instance as it received an unknown event. You might decide to throw an exception there.

### Aggregate with typed identity

The last abstraction is `Aggregate<T, TId>`, where `T` is `AggregateState` and `TId` is the identity type. You can use it if you want to have a typed identity. We provide a small identity value object abstraction, which allows Eventuous to understand that it's indeed the aggregate identity.

#### Aggregate identity

Use the `AggregateId` abstract record, which needs a string value for its constructor:

```csharp
record BookingId(string Value) : AggregateId(Value);
```

The abstract record overrides its `ToString` to return the string value as-is. It also has an implicit conversion operator, which allows you to use a string value without explicitly instantiating the identity record. However, we still recommend instantiating the identity explicitly to benefit from type safety.

#### Aggregate state with typed identity

The aggregate with typed identity also uses the aggregate state with typed identity. It's because the identity value is a part of the aggregate state.

A typed state base class has its identity property built-in, so you don't need to do anything in addition. The `BookingState` example above uses the typed state and, therefore, is able to set the identity value when it gets it from the event.

As we know what the aggregate identity is when using aggregates with typed identity, the `GetId` function is implemented in the base class. Therefore, there are no more abstract methods to implement in derived classes.

Although the number of generic parameters for this version of the `Aggregate` base class comes to three, it is still the most useful one. It gives you type safety for the aggregate identity, and also nicely separates state from behaviour.

Example:

```csharp
class Booking : Aggregate<BookingState, BookingId> {
    public void BookRoom(
        BookingId id,
        string roomId,
        StayPeriod period,
        decimal price
    ) {
        EnsureDoesntExist();
        Apply(new RoomBooked(
            id, roomId, period.CheckIn, period.CheckOut, price
        ));
    }

    public void Import(BookingId id, string roomId, StayPeriod period) {
        Apply(new BookingImported(
            id, roomId, period.CheckIn, period.CheckOut
        ));
    }
}
```
