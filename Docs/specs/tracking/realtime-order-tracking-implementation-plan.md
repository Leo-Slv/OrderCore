# Implementation plan — Real-time order tracking

Implements `Docs/specs/tracking/realtime-order-tracking.md`. Builds on
the order lifecycle events of `Docs/specs/events/async-messaging-implementation-plan.md`
(Stage 3) and instruments itself like
`Docs/specs/observability/observability-implementation-plan.md` says, so
it is implemented after both. Staged as usual: each stage leaves
`dotnet build`, `dotnet test` (all three projects) and
`dotnet format OrderCore.sln --verify-no-changes` passing and ends in its
own commit(s).

No decisions beyond the spec's were needed: everything below follows the
spec's decisions or an existing project convention.

## Shape

```text
backoffice "enviar" / storefront checkout / outbox confirming a payment
        │
        ▼
Order changes ── save ──► orders_outbox_messages ──► RabbitMQ (Eventos)
                                                        │
                                  Orders consumer "orders.realtime"
                                  (OrderUpdatesBroadcaster, Infrastructure)
                                                        │ IOrderUpdatesNotifier (Application contract)
                                                        ▼
                         SignalR OrderUpdatesNotifier (Presentation) ── IHubContext
                                                        │
                    group "customer:{customerId}"  and  group "admins"
                                                        ▼
                       tracking screen · storefront notices · live backoffice
```

- **Groups come from the token, never from the client.** On connect, a
  customer joins `customer:{customerId}` (from `ICurrentUser`), an admin
  joins `admins`. The client never asks to follow an order: a customer
  can only ever be in their own group, so they receive only their own
  orders' updates, and the tracking screen and the storefront notices
  are the same stream filtered by the page. Nothing to check per order,
  nothing to probe.
- **Layers.** The consumer lives in Orders' `Infrastructure` (like the
  payment-outcome handlers) and talks to an Application contract,
  `IOrderUpdatesNotifier`, implemented in `Presentation/Realtime` with
  `IHubContext` — the same "Presentation implements an Application
  abstraction" shape as `HttpContextCurrentUser`.

## Stage 1 — Shipment details

- Value object `ShipmentDetails` (carrier ≤ 100, tracking code ≤ 100,
  tracking URL absolute http(s) ≤ 2000; all optional, but a tracking
  code needs a carrier). `Order.Ship(now, ShipmentDetails?)` stores it;
  `OrderShipped` (domain) carries it.
- `POST orders/{id}/ship` accepts an optional body
  (`ShipOrderRequest { carrier, trackingCode, trackingUrl }`); no body
  keeps today's behavior. `OrderResponse` gains `shipment`.
- Persistence: three nullable columns on `orders`, migration
  `AddShipmentTracking`.
- The `OrderShipped` integration contract gains the optional carrier/
  tracking fields (additive, same version).

Tests: validation; an order shipped with and without details round-trips;
the customer sees the tracking on `GET orders/{id}`.

## Stage 2 — The hub

- `Modules/Orders/Presentation/Realtime/OrderUpdatesHub` mapped at
  `/api/hubs/orders`, `[Authorize]` (any signed-in user; anonymous
  connections are refused). `OnConnectedAsync` joins the groups above.
  The hub has no client-callable methods.
- Authentication: SignalR's WebSocket/SSE transports can't send an
  `Authorization` header from the browser, so `JwtBearerSetup` also
  reads the token from the `access_token` query string, **only** for
  requests under `/api/hubs`. The CORS policy already covers the
  storefront's origin for the negotiate request.
- `OrderUpdate` (the pushed message): `orderId`, `orderNumber`,
  `status`, `changedAt`, `customerId`, `totalAmount`, `currency`,
  `shipment` (when shipped). Client method name: `orderUpdated`.
- Single instance (decision 4): no backplane; `OrdersDependencyInjection`
  marks where `AddStackExchangeRedis` would go.
- Endpoint classification: the hub endpoint counts as a protected
  endpoint in `EndpointAuthorizationTests`.

## Stage 3 — From events to connections

- `OrderUpdatesBroadcaster` (`IIntegrationEventHandler<T>` for every
  Orders lifecycle contract, queue `orders.realtime`) maps the event to
  an `OrderUpdate` and calls `IOrderUpdatesNotifier.NotifyAsync`, which
  sends it to `customer:{customerId}` and `admins`. Idempotent like
  every consumer (Eventos' inbox); a customer with no connection simply
  receives nothing.
- Ordering: an update carries `changedAt`; the frontend ignores an
  update older than the state it already shows (documented for the
  frontend — messages can arrive out of order, spec section 21).
- Observability (Observability conventions): meter `OrderCore.Tracking`
  with open connections (by audience) and updates sent; the broadcast
  runs inside the consumer span, so a push is part of the order's trace.

## Stage 4 — Tests

Integration, with the SignalR .NET client against the test host
(`Microsoft.AspNetCore.SignalR.Client`, WebSockets through the test
server) and the RabbitMQ container:

- a customer connected before checkout receives `PendingPayment` →
  `Confirmed` for their order, in order;
- another connected customer receives nothing about it;
- an admin receives both, and a new order's first update;
- shipping with tracking pushes `Shipped` with the carrier and code;
- an anonymous connection and a connection with an expired token are
  refused;
- the token in the query string is accepted only on the hub path.

Unit: the event → `OrderUpdate` mapping; group names from the token.

## Stage 5 — Docs

- The hub contract (URL, authentication, the `orderUpdated` message and
  its fields, the reconnect-then-reload rule, ignoring older updates):
  documented in `README.md` — SignalR isn't part of the OpenAPI
  document, so this is the one place it can be described.
- `05-orders.md`: shipment details, hub, notifier, broadcaster.
- `ORDERCORE_CONTEXT.md`: real-time updates and the backplane note.
- `CLAUDE.md`: hubs live in the owning module's `Presentation/Realtime`,
  groups are derived from the token, and pushes are driven by events,
  never sent directly from a use case.
- Execution notes appended to this plan.

## Commits

1. `feat(orders): optional shipment details (carrier, tracking code, link)`
2. `feat(orders): SignalR hub for order updates with groups from the token`
3. `feat(orders): push order lifecycle events to connected customers and admins`
4. `test: real-time order updates over SignalR`
5. `docs: ...`
