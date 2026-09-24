# Storefront API — MVP (catalog → cart → checkout → order)

## What

Close the gaps that keep a customer-facing e-commerce frontend
(Next.js storefront: Home, `/products`, `/products/:slug`, `/cart`,
`/checkout`, `/orders`, `/orders/:id`) from being built on top of the
current API. Only the storefront's MVP path is in scope; account/login,
the backoffice (`/admin`) and payment/event views are later phases (see
"Out of scope").

The feature is a set of API-contract changes across the modules that
already own each concept — no new business module:

1. **Consistent error contract.** Every expected business failure
   (resource not found, insufficient stock, invalid state transition,
   validation error) reaches the client as an RFC 7807 `ProblemDetails`
   response with the correct 4xx status and a stable, machine-readable
   error code the frontend can branch on (e.g. show "this product became
   unavailable"). Today these failures surface as unhandled exceptions
   (HTTP 500), contradicting the `[ProducesResponseType]` declarations.
2. **Browser access.** The API accepts cross-origin requests from the
   configured storefront origin(s), so the frontend can call it from a
   different host/port.
3. **Product listing fit for a storefront (Catalog).**
   - Paginated with total count (the shared `PagedResponse<T>` shape
     already used by AuditLogs), so the UI can render page controls.
   - Sortable (at least: price ascending/descending, newest).
   - Filterable to "on sale" products (those with a compare-at price),
     which is what the Home "promoções" section needs.
   - Each list item carries what a product card shows: slug, category,
     currency, price and compare-at price, primary image, and stock
     availability.
4. **Product detail by slug (Catalog).** A product page is addressed by
   its slug (`/products/:slug`), not its internal id, and returns the full
   detail the page renders: descriptions, brand, category, all images
   (ordered, with alt text and which one is primary), variants, and stock
   availability. Only published/active products are reachable this way.
5. **Cart validation.** The cart itself lives in the client. The API
   offers a way to submit the cart's lines (product + quantity) and get
   back, per line, the current price, whether it is still purchasable and
   in stock for that quantity, and what changed compared to what the
   client last saw (price changed, product unavailable, insufficient
   stock), plus the cart total. This is what lets the frontend handle
   "preço mudou" / "produto ficou indisponível" without re-implementing
   the rules.
6. **Single-step checkout (Orders).** One request turns a validated cart
   into an order that is already awaiting payment: it receives the
   customer, the items, the chosen saved shipping and billing addresses
   (by id), the chosen payment method (card or Pix) and an idempotency
   key; it validates products, snapshots
   addresses, reserves stock and starts payment as one use case, and
   answers with the created order (status `PendingPayment`) without
   waiting for the payment outcome. Retrying with the same idempotency key
   never creates a second order. The frontend no longer orchestrates the
   create → set-addresses → request-payment sequence itself.
7. **Addresses usable at checkout (Customers).** Listing a customer's
   addresses returns the full address (recipient, phone, street, number,
   complement, neighborhood, city, state, postal code, country, default
   shipping/billing flags), so the checkout can display and select one.
8. **Order views a customer can follow (Orders).**
   - Order detail returns: order number, status, creation and status
     timestamps (confirmed, cancelled, shipped, delivered), the amount
     breakdown (subtotal, discount, shipping, tax, total, currency),
     shipping and billing addresses, items with SKU and image, and the
     current payment status and method — enough for the tracking screen to render
     "Processando pagamento…" / "Pedido confirmado" by polling.
   - The customer's order list is paginated and ordered newest first,
     each entry carrying number, date, status, total and item count.
   - The order's status history (already projected into
     `OrderStatusHistory`) is exposed so the tracking screen can draw its
     timeline from real transitions instead of guessing from the current
     status.
9. **OpenAPI stays accurate.** Every new or changed endpoint documents
   all its response codes and wire shapes, per the project's
   OpenAPI/Scalar convention.

## Why

The frontend is the first real consumer of OrderCore and the way its
architecture gets demonstrated end to end (produto → carrinho →
checkout → pedido → estoque → pagamento → acompanhamento). The analysis
of the current API against the planned screens showed:

- the product page, cart and account screens have no supporting endpoint
  at all, and the listing/order screens get responses too thin to render
  (no slug, no timestamps, no amount breakdown, no availability);
- checkout currently requires the client to chain three calls with no
  atomicity, which both breaks the "frontend asks, OrderCore decides"
  principle and can leave half-built orders behind;
- saved addresses cannot be displayed (the response carries only label
  and city), so they cannot be chosen at checkout;
- business errors surface as HTTP 500, so the frontend cannot tell
  "out of stock" from a server crash.

Fixing these on the backend keeps business rules (pricing, availability,
reservation, payment initiation) inside the modules that own them, and
the frontend a pure consumer of state.

## Out of scope (later phases)

- **Authentication and authorization** (register with a plain password
  hashed server-side, login, `me`, customer vs admin roles). In the MVP
  the customer is still identified by id in the request, as today. The
  current `PasswordHash`-from-client registration contract is a known
  issue to be fixed in that phase.
- Profile editing, address edit/removal/default selection.
- Backoffice endpoints: global order listing and status transitions
  (processing/shipped/delivered), stock listing/reservations/movements,
  payments listing/detail, customers listing, dashboard metrics.
- Shipping cost calculation, coupons/discounts, recommendations,
  best-sellers, banners.
- Push-based order updates (SignalR/WebSocket); the MVP relies on polling
  the order detail.
- Server-side persistent cart.

## Decisions (resolved with the user)

1. **Payment method is part of the MVP checkout.** The customer chooses
   card or Pix; the choice travels from the checkout to the `Payment`
   Payments creates, is persisted on it, and is returned wherever the
   payment status is shown. Both methods go through the same provider
   abstraction (`FakePaymentProvider` today), so no real Pix/card
   integration is part of this feature.
2. **Availability is exposed as a state, not a quantity.** The storefront
   sees `InStock` / `LowStock` / `OutOfStock` (low stock derived from the
   stock item's reorder level); exact on-hand/available quantities stay
   internal to Inventory and the backoffice. Cart validation reports
   "insufficient stock for the requested quantity" without revealing
   how many units remain.
3. **The existing multi-step order endpoints stay.** `POST orders`,
   `PUT orders/{id}/addresses` and `POST orders/{id}/request-payment`
   remain for manual/admin flows and tests; the storefront only uses the
   single-step checkout.
4. **`GET catalog/products` changes in place** to `PagedResponse<T>` —
   there is no consumer yet, and it keeps one listing endpoint consistent
   with `GET audit-logs`.
