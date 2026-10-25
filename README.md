# Order Events — ASP.NET Core + Kafka + Redis + SQL Server

A small but complete event-driven backend in **C# / .NET 9**, built the way the
services I work on day to day are built: a REST API that accepts work and
publishes an event, and a separate worker that consumes the event, decides the
outcome and writes it back.

The point of this repository is the plumbing, not the business domain — Kafka
offsets, cache invalidation, idempotency and graceful failure are the parts
worth looking at.

---

## What it does

```
POST /api/orders
      │
      ├─ 1. persist the order as Pending          (SQL Server, EF Core)
      ├─ 2. publish OrderPlaced                   (Kafka, key = order id)
      └─ 3. warm the cache                        (Redis)

                    orders.placed
                          │
                          ▼
              OrderEvents.Worker  (Kafka consumer)
                          │
      ├─ 1. decide Confirmed / Rejected           (pure business rules)
      ├─ 2. apply the outcome, once                (idempotent write)
      ├─ 3. invalidate the cached order            (Redis)
      └─ 4. publish OrderProcessed                 (Kafka)
```

`GET /api/orders/{id}` reads through Redis with a database fallback, so after
the worker runs the next read returns the new status rather than the cached
`Pending`.

---

## Decisions worth explaining

**Offsets are committed manually, after the handler succeeds.**
With `EnableAutoCommit`, a crash mid-handler advances the offset anyway and the
message is never reprocessed — silent data loss. Here a failed handler leaves
the offset uncommitted so the message is redelivered.
See `KafkaConsumerService.cs`.

**Redelivery is expected, so writes are idempotent.**
Kafka is at-least-once. Each order records the correlation id it was last
processed under; re-applying the same one is a no-op instead of a double write.
See `OrderRepository.TryApplyOutcomeAsync`.

**A malformed payload is committed past, not retried forever.**
A message that will never deserialize would otherwise block its partition
permanently. It is logged and skipped — a production system would route it to a
dead-letter topic first.

**A Redis outage degrades latency, not availability.**
Every cache call is wrapped; on failure the request falls through to the
database. `AbortOnConnectFail` is off so the app starts even if Redis is down.

**Invalidate before announcing.**
The worker clears the cache *before* publishing `OrderProcessed`, so anything
reacting to that event cannot read back the stale pre-processing copy.

**Persist before publishing.**
Publishing first can emit an event for an order that was never stored, which
the consumer then cannot find.

**The producer is a singleton.**
A Confluent producer opens broker connections; creating one per request
exhausts sockets under load.

---

## Project layout

| Project | Role |
| --- | --- |
| `OrderEvents.Contracts` | Event records, status enum and topic names shared by both sides |
| `OrderEvents.Infrastructure` | EF Core context + repository, Redis cache, Kafka producer and consumer base class, DI wiring |
| `OrderEvents.Api` | ASP.NET Core minimal API — validation, endpoints, OpenAPI |
| `OrderEvents.Worker` | Hosted Kafka consumer and the order rules |
| `OrderEvents.Tests` | xUnit tests for the rules, the repository and validation |

---

## Running it

### Everything, in Docker

```bash
docker compose up --build
```

Brings up Kafka (KRaft, no ZooKeeper), Redis, SQL Server, the API on
<http://localhost:8080> and the worker. Health checks gate startup so the
services do not race the broker and the database.

Create an order, then read it back a second later — status moves
`Pending → Confirmed`:

```bash
curl -X POST http://localhost:8080/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"cust-1001","sku":"KEYBOARD-87","quantity":2,"unitPrice":2499.50}'
```

Quantity over 100, or a total over 50,000, is rejected instead — the rule lives
in `OrderProcessor.Decide`.

### Just the API, no Docker

```bash
dotnet run --project src/OrderEvents.Api
```

The `Development` profile uses the EF in-memory provider and switches Kafka and
Redis off, so the API runs with nothing installed. Events are logged instead of
published. API at <http://localhost:5080>, OpenAPI at `/openapi/v1.json`.

`requests.http` has the full set of calls, including the validation and
not-found cases.

### Tests

```bash
dotnet test
```

22 tests covering the order rules (including both threshold boundaries),
repository behaviour — idempotent redelivery, unknown orders, ordering — and
request validation.

---

## Configuration

Every setting can be overridden with environment variables using `__` for
nesting, which is how `docker-compose.yml` configures both services.

| Setting | Default | Notes |
| --- | --- | --- |
| `Database:Provider` | `SqlServer` | `InMemory` for local runs and tests |
| `Database:ConnectionString` | — | SQL Server connection string |
| `Kafka:Enabled` | `true` | `false` logs events instead of producing them |
| `Kafka:BootstrapServers` | `localhost:9092` | |
| `Kafka:ConsumerGroupId` | `order-events-worker` | Worker only |
| `Redis:Enabled` | `true` | `false` sends every read to the database |
| `Redis:ConnectionString` | `localhost:6379` | |
| `Redis:CacheSeconds` | `60` | Cached order TTL |

The SQL Server password in `docker-compose.yml` is a throwaway local
development value. Real credentials belong in environment variables or a secret
store, never in the repository.

---

## Notes

- Targets .NET 9 and rolls forward, so it also runs on a newer installed runtime.
- The schema is created at startup with `EnsureCreated`. A production service
  would use EF Core migrations.
- Built by [Harshad Chaudhary](https://harshadchaudhary.github.io/portfolio/) —
  Junior .NET Backend Developer.
