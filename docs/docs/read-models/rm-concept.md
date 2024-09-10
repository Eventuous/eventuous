---
title: "Concept"
description: "The concept of read models"
---

## Queries in event-sourced systems

As described [previously](../domain/aggregate.md), the domain model is using [events](../domain/domain-events.md) as the _source of truth_. These events represent individual and atomic state transitions of the system. We add events to [event store](../persistence/event-store.md) one by one, in append-only fashion. When restoring the state of an aggregate, we read all the events from a single stream, and apply those events to the aggregate state. When all events are applied, the state is fully restored. This process takes nanoseconds to complete, so it's not really a burden.

However, when all you have in your database is events, you can hardly query the system state for more than one object at a time. The only query that the event store supports is to get one event stream using the aggregate id. In many cases, though, we need to query using some other attribute of the aggregate state, and we expect more than one result. Many see it as a major downside of Event Sourcing, but, in fact, it's not a big problem.

When building an event-sourced system, after some initial focus on the domain model and its behaviour, you start to work on queries that provide things like lists of objects via an API, so the UI can display them. There you need to write queries, and that's where the idea of CQRS comes in.

## CQRS (do you mean "cars"?)

The term CQRS was coined more than a decade ago by [Greg Young](https://twitter.com/gregyoung), who also established a lot of practices of Event Sourcing as implemented by Eventuous.

:::note
**CQRS** stands for **C**ommand-**Q**uery **R**esponsibility **S**egregation.
:::

The concept can be traced back in time to a separation between operational and reporting store:

> [The main] database supports operational updates of the application's state, and also various reports used for decision support and analysis.
> The operational needs and the reporting needs are, however, often quite different - with different requirements from a schema and different data access patterns. When this happens it's often a wise idea to separate the reporting needs into a reporting database...
> 
> [ReportingDatabase](https://martinfowler.com/bliki/ReportingDatabase.html) - Martin Fowler's bliki

Greg argues that it's not a requirement to separate two databases, but it's a good idea to at least understand that the need for transactional updates requires a different approach compared with reporting needs. Say, you use something like EntityFramework to persist your domain entities state. Although it works quite well, it's not a good idea to use it for reporting purposes. You'd be limited to reach the data using EntityFramework's DbContext, when in reality you'd want to make more direct queries, joining different tables, etc.

:::info What's up with CARS?
Where "did you mean CARS?" comes from? When CQRS wasn't as popular term, Google search assumed you made a mistake and proposed to search for "cars" instead.
:::

### CQRS and Event Sourcing

In real life, CQRS in event-sourced system means that you will have to separate the operation and the reporting stores. It is because querying the state of a single aggregate is not the only query you'd like to do. You might want to query across multiple aggregates, or across different aggregate types. In addition, you don't always need to return the full aggregate state, but only a subset of it.

That's where read models come in. Read models are _projections_ of the system state, which are built based on the query needs. Therefore, we sometime reference them as _views_, or _query models_. You'd normally use some other database than your event store database for storing read models, and that database needs to support rich indexing and querying.

## Benefits of read models

In state-based systems you normally have access to the state of your domain object in a very optimized, normalized schema. When executing a query over a normalized database, you'd often need to build a comprehensive set of joins across multiple tables or collections, so you can get all the required information in one go. That approach is not always optimal. Let's say you want to display a widget that shows the number of reservations made for a given hotel during the last 30 days. You'd need to run a count query across the reservations table, and then a join across the hotels table to get the hotel name.

Now imagine all the reservations made are represented as events. By _projecting_ those events to a read model that just calculates the number of reservations made for the last 30 days per hotel, you can get the same result in a much more efficient way. When you have a read model, you can do the same query in a single query, without the need to build joins. You'd just need to run a query against the read model, and it would return the required information in a single query, just using the hotel id as a single query argument.

You could see this approach as the normalization of an operational database schema. However, it's not the only thing that happens. When building read models, you are no longer bound to the primary key of the aggregate that emits state transitions. You can use another attribute as the primary key, or even a composite key. For example, with the number of reservations of a hotel, you could use the hotel id and the date of the reservation as the read model primary key.

The point here is that when building read models, you'd normally start designing them based on the needs of the query, not the needs of the database schema. The query needs most often come from the user interface requirements for data visualizations, which are often orthogonal to the operational needs of the domain model. Read model allows you to find a balance between operational and reporting needs without sacrificing the explicitness of the model for the richness and effectiveness of the query model.

Here are some examples of the read models that can be built for a given domain model:
- My reservations (per guest)
- My past stays (per guest)
- My upcoming stays (per guest)
- Upcoming arrivals (per hotel)
- Cancellations for the last three months (per hotel)

Built as read models, all those queries can be run in a single query, without the need to build joins over multiple tables and potentially thousands of rows or documents.

## How to build a read model?

For building read models, you need to receive events from the event store and project them real time to a queryable store. Let's say that we have two event types:

```
record RoomBooked(
    string BookingsId,
    string RoomId,
    string GuestId,
    DateOnly CheckIn,
    DateOnly CheckOut,
    float Price
);

record BookingCancelled(
    string BookingId,
    string Reason
)
```

We want to project those events to MongoDB, so we can issue queries across all the bookings. It can be done by projecting all the `RoomBooked` events to a `Bookings` collection. The document structure would be identical to the event contract for now.

So, we can create a subscription and add an event handler that would create a new `Booking` document in the collection when it receives an `RoomBooked` event. It would use the `BookingId` as the document id (`_id`) field in MongoDB. When using Eventuous, it's important to remember that it does not enforce _exactly one_ event processing rule (although it can), as it would have a negative impact on the subscription's performance. Therefore, when the handler has processed an event, it might eventually need to process it again when the application restarts after a crash. It might sound a bit scary, but in reality, those events will be delivered again in the same order, and it's easy to mitigate the issue by ensuring that the projection handler is idempotent. For our example, we could do that by using `updateOne` operation with the option `isUpsert` set to `true` instead of using the `insertOne` operation. Any update operation is by definition idempotent as long as it doesn't use operations on the existing state like `inc` or `dec`. That's why it's essential to only use event properties in updates, so the event needs to contain enough information for the projecting handler to execute the update without using the current projected document state.  

If we decide that we don't want to have cancelled bookings in that collection, we would add a new event handler to the same subscription. This new handler would remove the document from the collection when it receives `BookingCancelled` and use the `Eq` filter, so it deletes the document where the document id equals the `BookingId` property of the event.

## Consistency concerns

The data in a query database is updated when the projecting subscription receives and processes the event. It means that there's a delay between the operation is completed on the command side, when the result is returned to the caller, and the read model update. Under normal circumstances, this delay doesn't exceed a couple of hundred milliseconds. However, when the query store is unable to process updates as fast as the events arrive, the delay starts to increase (see [Mind the Gap](../subscriptions/subs-diagnostics)).

Due to this delay, the users of your system might experience a situation that when they submit a command and get completion the result back, the query doesn't return the latest changes because these changes haven't propagated to the read model yet. In the context of Event Sourcing, this phenomenon is often called _eventual consistency_.

However, the definition of eventual consistency has little in common with what's described above. According to [Wikipedia](https://en.wikipedia.org/wiki/Eventual_consistency), eventually-consistent services are often classified as providing BASE semantics (basically-available, soft-state, eventual consistency). The original meaning of eventual consistency implies that there's a distributed system with multiple nodes accepting writes, and those nodes need to get some operational slack for replicating changes mutually. However, subscriptions in event-sourced systems don't use this model. Projecting events cannot even be called "replication" as such. Instead, projections transform each event to a state update, and execute the state update transactionally in another database. These updates are also executed sequentially, and, when following best practices, replaying a set of events again would result in idempotent updates. In this sense, the read store cannot be in a state of conflict (as there's only one), and it can't contain _invalid_ data, but it can contain _stale_ data.

Another point of criticism of the potential staleness of read models is about RYW (read your writes) session guarantee. The claim here is that when you execute a command (write) and then run a query immediately after (read), you might get a stale result, so you don't see your write. Outside the scope of read models this claim is nonsense. For example, if you execute two consecutive writes in an event-sourced system, the second write will first read the result of the previous write, and the result will never be stale. It's because all the commands in a properly built event-sourced system use events as the source of truth, and they always read from the event store, which is a transactional, fully consistent database.

In fact, when a larger system with several components uses the same event store that implements the concept of a global sequential append-only event log, and when handling commands all the entities are solely retrieved from event streams, the system will exhibit characteristics of a strong consistency type called _sequential consistency_.

:::tip
You can read more about different consistency types [here](https://jepsen.io/consistency).
:::

It is, therefore, important to understand that in CQRS world you'd need to deal with more than one system component, and more than one database (when we talk about Event Sourcing). Even if you build a single, monolithic application, you'll find yourself dealing with issues similar to those normally found in distributed systems, and those issues need to be worked with using methods and practices that are established in the distributed world. 

## Dealing with stale data

There are a few aspects of dealing with stale data that you'd need to consider when building read models, and exposing queries on top of them.

### Is it a problem?

The first question to ask is exactly this: "is it really a problem?" For example, many systems today are built in a form-list fashion. You see a list of things, and there's an "Add" button there. When you click on it, you get a form. After filling out the necessary details in the form, you click a "Submit" or "Save" button, then you get redirected back to the same list. Naturally, if the list is fed by a query to the read model store, and the subscription for the read model projection takes 200ms to process one event, but the redirect takes 10ms, you will not find the new entry in the list.

The question here is if this is a good user experience at all when you need to search for a new entry in the potentially long list when you just added it. There are quite a few ways to provide a better experience, and the most popular one is to present the user with the new entry in read-only mode and there have a link "Back to the list".

Many systems expose multi-stage forms, where each stage is a logically-complete step in some workflow. There, you'd prefer to send a command for each stem in the flow. By doing that you eliminate the risk that the user will get a failure and lose all the entered data if a single "Submit" step fails. You'd also have at least some information projected to the read model as the user goes through the steps.

Other kinds of systems don't use lists as the primary entry point for their users. Think about hotel bookings and flight reservations. You fill out a form, get through the payment process (by that time your booking is already stored and awaits for the payment update), and then you get a "Thank you" screen with the booking number. You might never see the list of your flight reservations right after that as it's often a completely different part of the system where you need to navigate manually.

If you build a system that can behave without a "classic" (but often horrible) list-form flow, you might not need to care about your read models being stale as a normally functioning projection will get the read model updated way before the user gets to it.

### Define an SLA

In some cases, you have a requirement that the query model needs to be updated _immediately_ after the command has been executed. The "immediate" feeling urges the developer to start optimizing things. However, you should be asking "what _immediately_ means", and this question needs to be addressed to domain experts. Most often than not, after taking some time to think, they can provide a meaningful SLA instead of "immediately", as _nothing_ happens "immediately" anyway. Within the defined SLA you might need to optimize things, but the level of effort might be not as significant as you originally anticipated. You can also set up proper monitoring and alerting for measuring the projection staleness within the SLA. Eventuous provides enough tools out of the box to do that.

### Stop forgetting things

Not all user interfaces are built stateless. With the rise of single-page application frameworks such as React and VueJS, the user's browser holds quite a lot of state. That state can be used for remembering things. Think about that form again, haven't you got the [new entity state](../application/app-service.md#result) from Eventuous after calling the HTTP API? Why can't it be used to update the existing client-side application state instead of querying it from the server again? When using state management tools like Redux or VueX you can even propagate events received in the `Result` object to the client-side application state using the store reducers (which are, effectively, event handlers). This way, you can even improve the cohesiveness of the whole system by letting its front- and back-end to use the same events, using the same Ubiquitous Language.

### Wait

Sometimes you can't control the UI, but you do control the query API, and you know that the UI works in the form-list fashion. There's a clear risk that when the user gets redirected to the list after submitting the form, they won't find the new or updated information in the list. In that case, you can use the command handling result combined with the projected item `Position` property. For example, the [MongoDB projection](../infra/mongodb) implicitly updates the read model document `Position` property with the projected event global log position. When the command is handled successfully by the command service, you get the `OkResult` record instance back. There, you find the `StreamPosition` property, which points to the last appended event global position. You can then query your read model store for a specific read model that feeds the list where the user will be redirected to. When you find out that the document in that read model got updated with the `Position` property higher or equal the returned `StreamPosition` value, you can return `200 OK` result to the API call. Until then, you just wait. By doing this, you will ensure that the list that the user will see after handling the command will contain the updated information.

You can also query the checkpoint store for a given read model to see if the stored checkpoint surpasses the one you get in the `OkResult` object. But then, you need to be sure that the subscription is listening to the global event stream (it won't work if you use, for example, the category stream in EventStoreDB), and the checkpoint is not batched (it's batched by default). We don't recommend using this approach.
