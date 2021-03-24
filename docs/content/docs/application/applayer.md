---
title: "Application layer"
description: "Answers to frequently asked questions."
lead: "Answers to frequently asked questions."
date: 2020-10-06T08:49:31+00:00
lastmod: 2020-10-06T08:49:31+00:00
draft: false
images: []
menu:
  docs:
    parent: "application"
weight: 410
toc: true
---

The application layer sits between the system edge (different APIs), and the domain model. It is responsible for handling commands coming from the edge.

## Concept

In general, the command handling flow can be described like this:

1. The edge receives a command via its API (HTTP, gRPC, SignalR, messaging, etc).
2. It passes the command over to the application service. As the edge is responsible for authentication and some authorisation, it can enrich commands with user credentials.
3. The command service, which is agnostic to the API itself, handles the command, and gives response to the edge (positive or negative).
4. The API layer then returns the response to the calling party.

Eventuous gives you a base class to implement command services in the application later: the `ApplicationService`.

