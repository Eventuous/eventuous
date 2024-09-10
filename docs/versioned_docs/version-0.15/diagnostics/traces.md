# Distributed tracing

Eventuous uses .NET `Activity` API to trace operations for command services, persistence, subscriptions, and producers. When OpenTelemetry integration is enabled, the traces are exported to the configured exporter.

## Command service tracing

Command handling operation creates a span with the name that is a combination of the command service name and the command type. For example, a service called `BookingsCommandService` processing a command `BookRoom` would create a span `service.bookingscommandservice/bookroom`. The span is tagged with the command name attribute as well. The tag name is `eventuous.command`.

If the command handler fails, the span will be set as failed and the exception will be attached to the span.

## Persistence tracing

Persistence operations create a span with the name that is a combination of the operation name and the stream name. For example, appending events to the stream `Booking-128` would create a span `eventstore.append/booking-128`. The span is tagged with the operation name and the stream name attributes. The tag names are `db.operation` and `eventuous.stream`. In addition, the `db.system` tag is set to `eventstore`.

If the operation fails, the span will be set as failed and the exception will be attached to the span.

## Producer tracing

Producers operations create a span with the name `produce`. The span is tagged with the following attributes:

* `eventuous.stream`: name of the stream, topic or exchange where the message is produced to
* `message_type`: type of the message, only when one message is produced
* `message_id`: ID of the message, only when one message is produced
* `messaging_destination`: same value as `eventuous.stream`
* `messaging.message_id`: same value as `message_id`
* `messaging.destination_kind`: type of the destination, for example `stream`, `exchange`, etc.
* `messaging.system`: name of the messaging system, for example `rabbitmq`, `eventstoredb`, etc.
* `messaging.operation`: operation name, for example `produce`, `append`, etc.

## Subscription tracing

Subscription tracing is described in the [subscriptions diagnostics](../subscriptions/subs-diagnostics/index.md#subscription-tracing) page.