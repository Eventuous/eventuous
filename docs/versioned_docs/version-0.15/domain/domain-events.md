---
title: "Domain events"
description: "Domain events: persisted behaviour"
sidebar_position: 3
---

## Concept

If you ever read the [Blue Book](https://www.domainlanguage.com/ddd/blue-book/), you'd notice that the `Domain Event` concept is not mentioned there. Still, years after the book was published, events have become popular, and domain events in particular.

Eric Evans, the author of the Blue Book, has added the definition to his [Domain-Design Reference](https://www.domainlanguage.com/ddd/reference/). Let us start with Eric's definition:

> Model information about activity in the domain as a series of discrete events. Represent each event as a domain object. [...]
> A domain event is a full-fledged part of the domain model, a representation of something that happened in the domain. Ignore irrelevant domain activity while making explicit the events that the domain experts want to track or be notified of, or which are associated with state changes in the other model objects.

When talking about Event Sourcing, we focus on the last bit: "making explicit the events [...], which are associated with state changes." Event Sourcing takes this definition further, and suggests:

> Persist the domain objects state as series of domain events. Each domain event represents an explicit state transition. Applying previously recorded events to a domain objects allows us to recover the current state of the object itself.

We can also cite an [article](https://suzdalnitski.medium.com/oop-will-make-you-suffer-846d072b4dce) from Medium (a bit controversial one):

> In the past, the goto statement was widely used in programming languages, before the advent of procedures/functions. The goto statement simply allowed the program to jump to any part of the code during execution. This made it really hard for the developers to answer the question “how did I get to this point of execution?”. And yes, this has caused a large number of bugs.
> A very similar problem is happening nowadays. Only this time the difficult question is **“how did I get to this state”** instead of “how did I get to this point of execution”.

Event Sourcing effectively answers this question by giving you a history of all the state transitions for your domain objects, represented as domain events.

So, what this page is about? It doesn't look like a conventional documentation page, does it? Nevertheless, let's see how domain events look like when you build a system with Eventuous.

```csharp title="BookingEvents.cs"
public static class BookingEvents {
    public record RoomBooked(
        string RoomId,
        LocalDate CheckIn,
        LocalDate CheckOut,
        decimal Price
    );

    public record BookingPaid(
        decimal AmountPaid,
        bool PaidInFull
    );

    public record BookingCancelled(string Reason);

    public record BookingImported(
        string RoomId,
        LocalDate CheckIn,
        LocalDate CheckOut
    );
}
```

Eventuous do's and dont's:
- **Do** make sure your domain events can be serialized to a commonly understood format, like JSON.
- **Do** make domain events immutable.
- **Do** implement equality by value for domain events.
- **Don't** apply things like marker interfaces (or any interfaces) to domain events.
- **Don't** use constructor logic, which can prevent domain events from deserializing.
- **Don't** use value objects in your domain events.

Some of those points look like limitations, and they are. For example, avoiding value objects inside domain events primarily caused by lack of separation between domain events and persisted (stored in an [event store](../persistence/event-store.mdx)) events. It creates a requirement for the domain events to be fully (de)serializable, and it's not always possible when using value objects with their explicit validation rules. You also cannot use standard types like immutable arrays or lists as they cannot be deserialized.

It's a technical limitation which will be addressed soon.
