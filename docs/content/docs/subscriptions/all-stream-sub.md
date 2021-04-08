---
title: "All stream subscription"
description: "Subscribe to all events in the store"
date: 2021-04-08
lastmod: 2021-04-08
draft: false
images: []
menu:
  docs:
    parent: "subscriptions"
weight: 530
toc: true
---

Subscribing to all events in the store is extremely valuable. This way, you can build comprehensive read models, which consolidate information from multiple aggregates. You can also use such a subscription for integration purposes, to convert and publish integration events.

{{% alert icon="👉" %}}
[Read more](https://zimarev.com/blog/event-sourcing/all-stream/) about benefits of using the global event stream.
{{%/ alert %}}

## All stream subscription service

The `AllStreamSubscriptionService` class inherits from the [subscription service]({{< ref "sub-service" >}}).

Although the `AllStreamSubscriptionService` class is not abstract, we do not recommend using it directly. You need to create your own class, which inherits from `AllStreamSubscriptionService`. This way, you can specify, which event handlers that specific subscription service will serve.

WIP



