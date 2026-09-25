# Backoffice API (V2, part 2)

## What

The API behind the `/admin` screens of the frontend (dashboard, orders,
products, inventory, payments, customers). Every endpoint here is
admin-only, which depends on `Docs/specs/identity/authentication-and-account.md`
landing first.

1. **Orders.**
   - A global, paged order list filterable by status, customer and date
     range, newest first, showing number, customer, total, status and
     payment status.
   - Order detail for admins: everything the customer sees, plus internal
     notes, the customer's identity, the stock reservations of the order
     and the payment (provider, provider reference, failure reason,
     refunds).
   - Fulfilment transitions that exist in the domain but that nothing can
     trigger today: start processing, ship, deliver — each recorded in the
     status history (today only created/payment requested/confirmed/
     cancelled/payment failed are).
   - Cancelling an order as an admin, with a reason, including orders
     that were already paid (see open decision on refunds).
   - Setting internal notes.
2. **Payments.** A paged payment list (by status, method, date), payment
   detail with its refunds, and requesting a refund. Payments get
   captured as part of the order lifecycle (see open decision on when):
   today nothing in the normal flow ever captures an authorized payment.
3. **Inventory.**
   - Creating the stock record for a product (today there is no way to do
     it through the API at all — the MVP tests seed it in the database).
   - Receiving stock and adjusting it with a reason (adjust exists).
   - A paged stock list with on-hand, reserved and available quantities
     and low-stock state, filterable to "low stock" / "out of stock".
   - Setting a stock item's reorder level, which is what makes the
     storefront's `LowStock` state real (it is always 0 today).
   - The reservations and stock movements of a product (the history
     `StockMovementRecorder` already records).
4. **Catalog.** The product operations that exist in the domain but have
   no endpoint: change price (keeps the price-change audit/event),
   set/clear compare-at price (promotions), discontinue, add/remove/
   reorder images, add/remove variants. Admins list products including
   drafts.
5. **Customers.** A paged customer list with search by name/e-mail, and
   customer detail with their orders; deactivating/reactivating a
   customer.
6. **Dashboard.** Summary figures for a period: order count by status,
   revenue (sum of confirmed-and-beyond orders), new customers, number of
   low-stock and out-of-stock products, and the most recent orders.
7. **Audit log.** The existing audit-log listing, filterable by entity
   (e.g. every entry for one order) and by actor — this is the "event
   timeline" of the order detail screen.

## Why

The MVP made it possible to *buy*; nothing yet lets anyone *run the
shop*. Orders stop at `Confirmed` forever because nothing ships them,
authorized payments are never captured, stock can only be created by
writing to the database, promotions can't be set, and there is no view
of what is happening across customers. These are also the screens that
show off the parts of OrderCore that aren't a CRUD — the order and
payment state machines, reservations, the outbox and the audit trail —
which is the reason the backoffice exists in the frontend plan at all.

## Out of scope

- Real shipping integration (carriers, tracking numbers, labels) —
  "ship" is a state transition with a timestamp.
- Returns/RMA, partial shipments, editing an order's items after
  checkout.
- Exports (CSV/Excel), scheduled reports.
- Push updates to the admin screens (polling, as in the storefront).
- Category management beyond what exists (create/list).

## Decisions (resolved with the user)

1. **Payments are captured when the order ships.** Authorization holds
   the money while the order is confirmed and being prepared; marking the
   order as shipped captures it. If capture fails, the order does not
   move to Shipped and the admin sees why.
2. **Cancelling a paid order settles the payment automatically.** In the
   same operation: an authorized-but-not-captured payment is released so
   the buyer is never charged, a captured one is refunded in full, and
   the stock reservation is released (or, if already consumed, the stock
   goes back). The order can't end up cancelled with the money kept.
3. **The dashboard is computed on demand**, each figure aggregated from
   the owning module's data through that module's Application layer — no
   read-model projections to keep in sync.
4. **Reorder level is set per stock item by an admin.** It starts at 0
   (never "low") until someone sets it.
5. **The stock screen is served by the catalog.** The admin product list
   shows each product's stock figures and filters by low/out of stock,
   so the screen has names and SKUs without the inventory having to know
   about products.
6. **Every product has a stock record automatically.** Creating (or
   publishing) a product ensures its stock record exists with zero units;
   admins only receive and adjust stock, never "create" it.
7. **The audit log is persisted.** The order's event timeline has to
   survive a restart, so the audit log moves from memory to the database
   as part of this feature.
