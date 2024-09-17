---
title: "Compare"
description: "How Eventuous is different from others?"
---

## How is Eventuous different from other libs?

You might have seen other libraries, old and new, that provide similar functionality to Eventuous. Here's how Eventuous is different:

### Persistence

Following EventStoreDB principles, Eventuous is build with strict global ordering in mind. It has pros and cons, and we believe that the pros outweigh the cons.

In real life events happen in certain order. Naturally, some things happen at the same time, and it is literally impossible to say if two events happened at the same time or one before another. And, we also would not care, **unless** there's a causation. There are many debates on this topic out there, where the primary argument against caring for global order is that we should only care about order of events in the same stream. This is a valid point, but it's not the only one. Events across streams might have causality, and we should care about it.

The problem is that causal relationship is not easy to implement, and it's often a domain concern that cannot be abstracted to a library. However, Eventuous provides a way to implement causal _ordering_ between events in different streams by enforcing global ordering.

As a consequence, causal ordering is available across streams without additional complexity from the domain model side.

However, this approach sets certain expectations for the underlying infrastructure. For example, EventStoreDB is a perfect match for Eventuous, as it provides the same guarantees. Other stores implemented by Eventuous provide the same guarantees but global ordering requirements might affect the performance.

### Just enough abstractions

Eventuous provides just enough abstractions to make it easy to use, but not too much to make it hard to understand. We believe that the best way to learn is to see how things work under the hood. That's why Eventuous is built with a clear separation of concerns, but without unnecessary abstractions.

Many libs and frameworks enforce abstractions on things that should not be abstracted. Mainly, it's about so-called _marker interfaces) like `IEvent` or `ICommand`. Eventuous does not enforce such interfaces, as they are not necessary for the library to work.

Additionally, interfaces like `IEventHandler` are often used for things like projections, or implicit invocation of `Apply<T>` methods on aggregates. Eventuous does not use such interfaces in favour of explicit mappings of types to functions, which avoids the library to do magic behind the scenes and doesn't require to use reflections, which would be bad for performance.

Eventuous uses reflections carefully and only during bootstrapping.

### Latest .NET

Eventuous is relatively new, and we aim keeping it up to date with the latest features of C# and .NET. The library uses ASP.NET Core native elements like configuration, hosting, and dependency injection. It allows developers to jump-start their projects without learning new concepts, at least about the bootstrapping basics.

### No strong DDD focus

Although Domain-Driven Design (DDD) is very useful and provides patterns that are directly applicable for event-sourced systems, there are different styles of building applications. Code patterns like Aggregate and Repository are quite old, and there's no necessity in using them everywhere. Eventuous provides a way to build event-sourced systems without enforcing DDD patterns. It's up to the developer to decide what building blocks to use.

### First-class observability

Eventuous implements all elements of observability like [logging](../diagnostics/logs.md), [metrics](../diagnostics/metrics.md), and [distributed tracing](../diagnostics/traces.md).

It is important to understand that observability is not only about logging and metrics. It's about understanding the system's behaviour and performance. Eventuous provides a way to trace messages across the system, which is crucial for understanding the system's behaviour. Following the current observability trends, Eventuous [integrates](../diagnostics/opentelemetry.md) natively with OpenTelemetry, so applications can be configured to export traces and metrics to any APM provider that support OTLP.

Particularly, each event-sourced system essentially is a distributed system, and it's important to understand how messages flow through the system. Eventuous provides a way to trace messages across the system, which is crucial for understanding the system's behaviour.