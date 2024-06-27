---
title: "Persistence"
description: >
    What about repositories and other questions about persistence
---

### Why you don't have repositories?

The definition of the `Repository` pattern from [bliki](https://martinfowler.com/eaaCatalog/repository.html):

> [Repository] Mediates between the domain and data mapping layers using a collection-like interface for accessing domain objects.

When using Event Sourcing, the idea of having a collection-like interface for accessing domain objects might be challenging to implement. It's because we don't persist domain objects state, but their state transitions as events instead. Therefore, having a collection-like abstraction on top of an event-sourced persistence would require, essentially, to load the whole event store to memory.

As Event Sourcing greatly benefits from using CQRS, the need to have a collection-like persistence abstraction also diminishes. In the command-oriented flow, you would only change the state of a single aggregate by handling one command. Therefore, you neither need a collection-like interface to load the state of a single aggregate, nor to append new events for a single aggregate to the store.
