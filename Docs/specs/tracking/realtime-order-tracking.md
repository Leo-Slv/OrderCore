# Real-time order tracking (V3, "Tracking")

## What

A customer following an order sees each change the moment it happens —
"Processando pagamento…" turning into "Pagamento aprovado", then
"Preparando pedido", "Enviado", "Entregue" — without the page asking the
API every few seconds.

1. **Live updates for the customer** (and, per the decisions below, for
   the storefront as a whole and the backoffice). While a signed-in
   customer has the tracking screen open, every change of their order's status reaches
   the browser as it happens (payment authorized or failed, confirmed,
   processing, shipped, delivered, cancelled). Polling
   `GET /api/orders/{id}` keeps working as a fallback, and is what the
   page uses to load its first state and to recover after a
   disconnection.
2. **Only your own orders.** A connection is authenticated with the same
   token as the API; a customer receives updates about their own orders
   only, and nothing about anyone else's — the same rule as the REST
   endpoints.
3. **Driven by the order's events.** Updates come from the order
   lifecycle events the Eventos feature publishes, not from a second
   path through the code: whatever changes an order (the storefront, the
   backoffice, the outbox confirming a payment) reaches the tracking
   screen the same way.
4. **What an update says.** Enough for the screen to move to the next
   step and show when it happened, without a second request for the
   common case (see the open decision on its content).
5. **Survives reconnects.** A browser that loses the connection and
   comes back doesn't miss a final state: it reconnects and reloads the
   order's current state.

## Why

- **It is V3 of the frontend plan** ("Tracking"), which already sketched
  the tracking screen as the place that shows the state machine and its
  intermediate states, starting with polling and "futuramente WebSocket
  ou SignalR".
- **The asynchronous flow makes the gap visible.** After checkout the
  order waits for the payment outcome to arrive through the outbox and
  RabbitMQ; polling shows it seconds late at best and costs a request
  per open page every few seconds.
- **It shows the architecture end to end:** a click in the backoffice
  ("enviar") becomes an event, goes through the broker, and appears on
  the customer's screen.

## Out of scope

- E-mail, SMS or push notifications to devices.
- Real carrier integration (tracking numbers fetched from a carrier,
  delivery events from a carrier's webhook).
- Estimated delivery dates.
- Offline delivery of missed updates beyond "reload the current state
  on reconnect".

## Decisions (resolved with the user)

1. **SignalR.** Native to ASP.NET Core, authenticated with the same
   token, automatic reconnection and transport fallback, and an official
   TypeScript client for the Next.js frontend.
2. **Three audiences:**
   - the **tracking screen**: the customer sees their order change live;
   - **storefront notices**: a signed-in customer is told, on any page,
     when one of their orders changes ("seu pedido foi enviado");
   - the **live backoffice**: admins see new orders and status changes
     appear in the order list and dashboard without reloading.
3. **Shipment details, optional.** Shipping an order may record the
   carrier, the tracking code and a tracking link, typed in by the
   admin; the customer sees them on the order and in the live update.
   No carrier integration.
4. **One API instance for now.** Live updates work within a single
   instance, as `docker compose` runs it; where a backplane (Redis,
   section 31) plugs in for horizontal scaling is documented, not built.
5. **A light summary per update:** order id and number, the new status,
   when it changed, the customer's id, total and currency, and — when
   shipped — carrier/tracking. It serves all three screens; the frontend
   reloads the full order only when it needs more.
6. **The backoffice dashboard reloads on updates.** Admins receive the
   order updates; the dashboard (computed on demand) reloads itself when
   one arrives, no more often than a minimum interval, instead of the
   server pushing recomputed figures.
