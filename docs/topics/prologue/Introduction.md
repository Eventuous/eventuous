<card-summary>What is Eventuous and why you want to use it for implementing 
an event-sourced system with .NET?</card-summary>
<title>Introduction</title>

## What is Eventuous?

Eventuous is a (relatively) lightweight library, which allows building production-grade applications using the [Event Sourcing](https://www.eventstore.com/blog/what-is-event-sourcing) pattern.

The base library <path>Eventuous.Domain</path> has a set of abstractions, following Domain-Driven Design tactical patterns, like [Aggregate](Aggregate.md).

Additional components include:

- [Persistence](Persistence.topic) using [EventStoreDB](EventStoreDB.md), [PostgreSQL](PostgreSQL.md),
  and [Microsoft SQL Server](MS-SQL-Server.md)
- [Real-time subscriptions](../subscriptions) for EventStoreDB, PostgreSQL, Microsoft SQL Server, RabbitMQ, and Google Pub/Sub
- [Command services](Application.topic) and HTTP-based commands
- Extensive observability, including Open Telemetry support
- Integration with ASP.NET Core dependency injection, logging, and Web API
- [Producers](../producers) for EventStoreDB, RabbitMQ, Google PubSub, and Apache Kafka
- [Read model](../read-models) projections for MongoDB
- [Gateway](../gateway) for producing events to other services (Event-Driven Architecture support)

> Eventuous is under active development and doesn't follow semantic versioning. We introduce changes often, according to immediate needs of its production users. The API hasn't reached a stable state and can change at any time. A patch version update would normally not change the API, but the minor version cloud.
>

### Packages

You can find all the NuGet packages by visiting the [Eventuous profile](https://www.nuget.org/profiles/Eventuous/).

Eventuous
: The umbrella package that includes the most used components (Domain, Application, Persistence). Doesn't include
subscriptions or any infrastructure support.

Eventuous.Domain
: Library that includes the [domain model](Domain.topic) abstractions like Aggregates.

Eventuous.Application
: [Command services](Application.topic) base library, including diagnostics and support for dependency injection.

Eventuous.Persistence
: The base library for [persistence](Persistence.topic), including event store and aggregate store abstractions. Also
includes support for dependency injection.

Eventuous.Subscriptions
: [Subscriptions](../subscriptions) base library, including diagnostics and support for dependency injection.

Eventuous.Subscriptions.Polly
: Support for retries in event handlers using [Polly](http://www.thepollyproject.org/).

Eventuous.Producers
: [Producers](../producers) base library, including diagnostics and support for dependency injection.

Eventuous.Diagnostics
: Diagnostics base library.

Eventuous.Diagnostics.OpenTelemetry
: Diagnostics integration with [OpenTelemetry](https://opentelemetry.io/).

Eventuous.Diagnostics.Logging
: Eventuous internal logs adapter for ASP.NET Core logging.

Eventuous.Gateway
: [Gateway](../gateway) component for connecting subscriptions with producers. Typically used for converting domain (
private) events to integration (public) events, as well as for implementing reactive processes.

Eventuous.EventStore
: Support for [EventStoreDB](EventStoreDB.md) (event store, subscriptions, producers).

Eventuous.Postgresql
: Support for [PostgreSQL](PostgreSQL.md) (event store, subscriptions, producers)

Eventuous.SqlServer
: Support for [Microsoft SQL Server](MS-SQL-Server.md) (event store, subscriptions, producers)

Eventuous.RabbitMq
: Support for RabbitMQ (subscriptions, producers).

Eventuous.GooglePubSub
: Support for Google Pub/Sub (subscriptions, producers).

Eventuous.GooglePubSub.CloudRun
: Support for Google Pub/Sub trigger for Cloud Run serverless workloads using HTTP.

Eventuous.Kafka
: Support for Apache Kafka (producers).

Eventuous.ElasticSearch
: Support for Elasticsearch (producers, event store for archive purposes).

Eventuous.Projections.MongoDB
: Projections support for [MongoDB](https://www.mongodb.com/).

Eventuous.Extensions.DependencyInjection
: Dependency injection extensions for command services, aggregate factory, etc.

Eventuous.AspNetCore.Web
: [HTTP API automation](../application/command-api) for command services.

Normally, for the domain model project, you would only need to reference `Eventuous.Domain` package.

## Go further

Read about [the right way](The-Right-Way.md) to understand how Eventuous embraces the original idea of Event Sourcing.

Have a look at the [samples](Samples.topic).

