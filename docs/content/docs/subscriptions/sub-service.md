---
title: "Subscription service"
description: "Base class for subscriptions"
date: 2021-04-08
lastmod: 2021-04-08
draft: false
images: []
menu:
  docs:
    parent: "subscriptions"
weight: 525
toc: true
---

Eventuous implements all subscription types as hosted services. It's because subscriptions need to start when the application starts, work in background when the application is running, then shut down when the application stops.

Eventuous has a `SubscriptionService` base class. Both `AllStreamSubscriptionService` and `StreamSubscriptionService` inherit from it as it handles most of the subscription mechanics, such as:

- Selecting event handlers, which the subscription will serve
- Reading the last known checkpoint
- Subscribing (this one is delegated to the implementation)
- Handling eventual subscription drops and resubscribes
- Updating the checkpoint
- Graceful shutdown

WIP



