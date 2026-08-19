# Bookings sample (KurrentDB)

A two-service hotel booking application demonstrating Eventuous with KurrentDB as the event store:

- **Bookings** — commands and queries for room bookings. Projects booking state to **MongoDB**
  (`BookingStateProjection`, `MyBookingsProjection`) and to **Azure Blob Storage**
  (`BookingStateBlobProjection`) from the same all-stream subscription, showing multiple projection
  targets side by side. The blob projection uses `ByGlobalPosition` idempotency and race retries.
- **Bookings.Payments** — records payments and publishes integration events back to KurrentDB
  through the Eventuous gateway; the Bookings service consumes them with a persistent subscription.

## Run with .NET Aspire

The `Bookings.AppHost` project orchestrates everything: KurrentDB, MongoDB, the Azurite blob
storage emulator, both services, and a Scalar API reference for browsing the APIs. Telemetry from
both services flows to the Aspire dashboard via OTLP.

```bash
aspire run
# or
dotnet run --project Bookings.AppHost
```

Requires the [Aspire CLI](https://learn.microsoft.com/dotnet/aspire/cli/install) (for `aspire run`)
and a container runtime such as Docker Desktop.

## Run standalone

Start the infrastructure, then run the services:

```bash
docker compose up -d
dotnet run --project Bookings        # listens on :5051
dotnet run --project Bookings.Payments
```

On Apple Silicon, edit `docker-compose.yml` to use the arm64 KurrentDB image (see the comment there).
The compose file also starts Zipkin, Prometheus, Grafana, and Seq for the observability tooling the
services use when no OTLP endpoint is configured.

## Try it

1. Book a room: `POST /booking/book` on the Bookings service.
2. Record a payment on the Payments service — the command endpoints are discovered from
   annotations; find them in the service's API reference (Scalar in Aspire, Swagger UI standalone).
3. Read the Mongo projection: `GET /bookings/my/{userId}`.
4. Read the blob projection: `GET /bookings/{bookingId}/view` — served from the `bookings` blob
   container, one JSON blob per booking (while `GET /bookings/{bookingId}` folds the state from
   the event stream).
