---
title: "State"
description: "Event-sourced immutable state"
sidebar_position: 2
---

## Event-sourced state

Eventuous has an abstraction for event-sourced state. The state can be used both as an aggregate state, or independently when using functions to handle commands and produce new events using the [functional service](../application/func-service.md). Moving along, we consider event-based state transitions as part of the state handling. Therefore, the state objects needs to expose an API to receive events and produce a new instance of itself (remember that the state is immutable).

To support state immutability, `State` is an abstract _record_, not class. Therefore, it supports immutability out of the box and supports `with` syntax to make state transitions easier.

A record, which inherits from `State` needs to implement a single function called `When`. It gets an event as an argument and returns the new state instance. There are two ways to define how events mutate the state, described below.

### Using pattern matching

Using pattern matching, you can define how events mutate the state with functions that return the new `State` instance.

For example:

```csharp title="BookingState.cs"
public record BookingState : State<BookingState> {
    decimal Price { get; init; }

    public override BookingState When(object @event)
        => @event switch {
            RoomBooked booked        => this with { Price = booked.Price },
            BookingImported imported => this with { Price = booked.Price },
            _                        => this
        };
}
```

The default branch of the switch expression returns the current instance as it received an unknown event. You might decide to throw an exception there.

Although it is possible to use pattern matching, we recommend using explicit handlers, as described below.

### Using explicit handlers

:::tip Use explicit handlers
Eventuous performs additional checks if event types, which are handled by the `When` function, are registered in the type map. If you use pattern matching, the check is impossible to perform, and the application can crash if the event is not registered in the type map.
:::

You can also use explicit event handlers, where you define one function per event, and register them in the constructor. In that case, there's no need to override the `When` function.

The syntax is similar to registered command handlers for the [command service](../application):

```csharp title="BookingState.cs"
public record BookingState : State<BookingState> {
    public BookingState() {
        On<RoomBooked>(
            (state, booked) => state with {
                Price = new Money(booked.Price),
                AmountPaid = new Money(0)
            }
        );

        On<BookingImported>(
            (state, imported) => state with {
                Price = new Money(imported.Price),
                AmountPaid = new Money(0)
            }
        );

        On<BookingPaymentRegistered>(
            (state, paid) => state with {
                AmountPaid = state.AmountPaid + new Money(paid.AmountPaid),
                _registeredPayments = state._registeredPayments.Add(
                    new Payment(paid.PaymentId, new Money(paid.AmountPaid))
                )
            }
        );
    }

    ImmutableArray<Payment> _registeredPayments = ImmutableArray<Payment>.Empty;

    public Money Price      { get; private init; }
    public Money AmountPaid { get; private init; }

    public bool IsFullyPaid()
        => AmountPaid.Amount >= Price.Amount;

    public bool IsOverpaid()
        => AmountPaid.Amount > Price.Amount;

    public bool HasPayment(string paymentId)
        => _registeredPayments.Any(p => p.PaymentId == paymentId);

    record Payment(string PaymentId, Money Amount);
}
```

As you can see, state is immutable, and the `On` function registers a handler for the event. The handler receives the current state and the event as arguments and returns the new state instance.

The sample code also shows that the state class can have some query logic, which is not related to the event handling. It can be useful to encapsulate queries in the state class so the domain logic gets only focused on making decisions based on the state.

