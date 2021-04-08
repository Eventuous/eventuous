---
title: "FAQ: Compare"
description: "How Eventuous is different from others?"
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "faq"
weight: 930
toc: true
---

## EventFlow

EventFlow is probably the most popular Event Sourcing and CQRS framework for .NET. It has a rich history, it's battle-tested and ready for production use. So, why not EventFlow?

### Abstractions

EventFlow is full of abstractions. There's an interface for everything, including commands and events. In fact, Eventuous discourages you to use interfaces for these primitives, as they are just serializable property bags. For example, a `Command` base class in EventFlow has 60 lines of code with three constrained generic parameters, one inheritance (`ValueObject`), and it implements a generic interface `ICommand`. In Eventuous, we recommend using records, but you can use classes too.

### Codebase size

We strongly believe in code, which fits in one's head. You can read all the Eventuous core library code in half an hour and understand what it does. There is no magic there. EventFlow, on the other side, has lots of interfaces and attributes that aim to prevent developers from doing the wrong thing. All those guide rails need code, which Eventuous doesn't have and will never have.

### Just enough

Unlike EventFlow, we don't provide anything special for logging, DI containers, scheduled jobs, sagas, etc. We know some open-source great tools to deal with these concerns, which are out of scope for this project. However, Eventuous plays well the any DI, as well as it uses Microsoft logging abstractions.
