---
title: "Compare"
description: "How Eventuous is different from others?"
---

## EventFlow

EventFlow is probably the most popular Event Sourcing and CQRS framework for .NET. It has a rich history, it's battle-tested and ready for production use. So, why not EventFlow?

### Abstractions

EventFlow is full of abstractions. There's an interface for everything, including commands and events. In fact, Eventuous discourages you to use interfaces for these primitives, as they are just serializable property bags. For example, a `Command` base class in EventFlow has 60 lines of code with three constrained generic parameters, one inheritance (`ValueObject`), and it implements a generic interface `ICommand`. In Eventuous, we recommend using records, but you can use classes too.

### Codebase size

We strongly believe in code, which fits in one's head. You can read all the Eventuous core library code in half an hour and understand what it does. There is no magic there. EventFlow, on the other side, has lots of interfaces and attributes that aim to prevent developers from doing the wrong thing. All those guide rails need code, which Eventuous doesn't have and will never have.

### Just enough

Unlike EventFlow, we don't provide anything special for logging, DI containers, scheduled jobs, sagas, etc. We know some open-source great tools to deal with these concerns, which are out of scope for this project. However, Eventuous plays well any DI, as well as it uses Microsoft logging abstractions.

## NEventStore

NEventStore is one of the first, if not the first library for Event Sourcing in .NET space. It is as old as the idea of Event Sourcing itself as we know it today. Quite a few people who are or were actively involved in defining what Event Sourcing is, have contributed to NEventStore. The latest version of NEventStore known at the date of writing this text is end of December 2021, and it's the version 9.0.1.

The library (it's not a framework) only focused on persistence. It is basically a layer on top of a regular operational database. The aim of NEventStore is to be as agnostic as possible to the underlying database. To achieve the desired level of abstraction, creators of NEventStore made some decisions that form a foundation of its design:

- The atomic write is a commit, which might be one or more events. All data in the commit is stored as a single object. There's no way to ensure you read of process individual events as NEventStore will always read commits.
- There's no concept of a global event log. In fact, it's quite common for persistent libraries on top of relational databases to avoid having the global event log, arguing that it's an anti-pattern.
- NEventStore assumes that systems are normally built as monoliths. This statement might sound strange as you can't find it in the architecture page of the documentation. However, there is an important concept of a dispatcher, which implements a flavour of the outbox pattern, and it aims to deliver events to subscribers in-process. For catch-up subscriptions, you can only find so-called "reference implementation" of the polling client, and it is located in a separate project.
- NEventStore persistence abstraction puts substantial effort to enable using almost any SQL or NoSQL store. The documentation makes it clear that it was the goal of the creators of NEventStore. However, most of the persistence implementations are for SQL databases. Only two NoSQL databases have support that's marked as "completed" - MongoDB and RavenDB.

Without the knowledge of the initial ideas of the creators of NEventStore it seems that the fundamental decisions were made to aim to support as many databases as possible. However, even if the goal of making it possible didn't seem to pay off as implementing similar functionality on top of a SQL database would be easier, and the only popular NoSQL database that is supported by the library is MongoDB.

This challenge is easy to spot when reading the architecture page of NEventStore docs:

> One other area where NEventStore has a distinct advantage is that of eventual consistency in the store itself. Because the storage engine is not dictated, an engine such as CouchDB, Cassandra, or Riak could be utilized which support multi-master replication.

Indeed, it can be a distinct advantage. Sadly, you cannot find any implementation of the persistence for any of those databases.

In contrast, Eventuous started with supporting EventStoreDB only, and the persistence design was built according to the EventStoreDB capabilities based on the assumption that it's a purpose-built database for Event Sourcing. It allows to keep the persistence API limited to a set of abstractions and serialization implementation, enriched with implementation of best practices like event type mappings, optimistic concurrency based on event stream revisions, and so on. When Eventuous received contributions to add Microsoft SQL Server and Postgres support, those implementations were able to implement the same API, and the persistence API didn't need to be changed. Core concept of asynchronous subscriptions are also supported for MS SQL and Postgres using continuous polling, which is a part of the implementation library that implements the subscriptions API of Eventuous.

Additional challenges of using NEventStore:

- The library maintains its own DI container and logging library. The user of NEventStore must implement their own wiring between those components and ASP.NET Core.
- All the IO operations are synchronous. There's no async support. Considering that most of the modern database drivers are async-first, and synchronous calls are just blocking wrappers around async calls, it's a significant drawback.

Eventuous was built with the modern .NET stack in mind. It's fully asynchronous and supports ASP.NET Core dependency injection container and logging abstractions.