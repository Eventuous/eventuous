---
title: "Say hello to Eventuous 👋"
description: "Introducing Eventuous, an opinionated lightweight library for .NET to build event-sourced applications."
lead: "Introducing Eventuous, an opinionated lightweight library for .NET to build event-sourced applications."
date: 2020-11-04T09:19:42+01:00
lastmod: 2020-11-04T09:19:42+01:00
draft: false
weight: 50
images: ["eventuous-logo.png"]
contributors: ["Alexey Zimarev"]
---

Honestly, I like heated debates on Twitter; I really do. Due to the nature of my work as a Developer Advocate at Event Store, I engage in many discussions about Event Sourcing. I hear a lot that the idea is great, but using it in real life is way too hard. As I am involved in building production systems, which are event-sourced from the start, I tend to disagree with this statement. I feel that people aren't familiar with some of the implementation details, scaring them off. Unfamiliarity is often mistaken as complexity.

In fact, I strongly believe that Event Sourcing removes a lot of complexity from business applications. You don't need to spend hours and days trying to figure out how your complex domain model can have its state persisted in a bunch of tables in a relational database. Neither would you get puzzled by the question "how my system got to this state", as the answer is in front of you.

Then I get people telling me: we need some baseline, which we can use to build something real. The more traditional persistence patterns accumulated lots of knowledge and experience dealing with common gotchas also represented in libraries and frameworks. Those tools give developers a solid foundation for their daily work. In the Event Sourcing world, we might feel uncomfortable also because we don't feel this presence of past knowledge implemented in code, which we can use.

That's how Eventuous was born. It is based on years of experience building production-grade systems using Event Sourcing in different industries. It doesn't have many guide rails, preventing you from making mistakes; that is not the goal. Eventuous gives you "just enough" bootstrap for your application.

I am using the library myself on a daily basis. That's where the changes and fixes are coming from, as I find issues or need a bit more. As I mostly use [EventStoreDB](https://eventstore.com) and MongoDB for persistence, that's supported out of the box. If you need more, consider contributing as I won't spend time building something I don't need.

Packages are available on NuGet, but there's no release version. All preview packages are reasonably stable. I don't plan to put a "stable" version out as I don't think the API would be stable enough any time soon.

If you are using Eventuous commercially, consider sponsoring it as an organisation. If you are an individual and just like the library, consider supporting it with a small amount. Even symbolic donations give me the feeling that I need to continue doing what I do. For consulting and paid support, reach out to me via [Ubiquitous](https://ubiquitous.no).
