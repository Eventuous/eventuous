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

Oh, that's it? A record? Yes, a record. Domain events are property bags. Their only purpose is to convey the state transition using the language of your domain. Technically, a domain event should just be an object, which can be serialised and deserialized for the purpose of persistence.

Eventuous do's and dont's:
- **Do** make sure your domain events can be serialised to a commonly understood format, like JSON.
- **Do** make domain events immutable.
- **Do** implement equality by value for domain events.
- **Don't** apply things like marker interfaces (or any interfaces) to domain events.
- **Don't** use constructor logic, which can prevent domain events from deserializing.
- **Don't** use value objects in your domain events.

The last point might require some elaboration. The `Value Object` pattern in DDD doesn't only require for those objects to be immutable and implement equality by value. The main attribute of a value object is that it must be _correct_. It means that you can try instantiating a value object with invalid arguments, but it will deny them. This characteristic along forbids value objects from being used in domain events, as events must be _unconditionally deserializable_. No matter what logic your current domain model has, events from the past are equally valid today. By bringing value objects to domain events you make them prone to failure when their validity rules change, which might prevent them from being deserialized. As a result, your aggregates won't be able to restore their state from previously persistent events and nothing will work.
