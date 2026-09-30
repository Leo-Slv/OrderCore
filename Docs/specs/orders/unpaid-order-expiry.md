# Unpaid orders don't hold stock forever (V5, part 2)

## What

An order that is waiting for payment but has no payment in progress stops
holding stock after a while, the same way an order whose buyer never
confirms the card already does (Stripe spec, decision 8).

1. **Today's gap.** Checkout reserves the stock and saves the order before
   it starts the payment. If starting the payment fails (the provider is
   down, the request times out) and the customer never repeats the
   checkout, the order stays `PendingPayment` with no payment, and its
   units stay reserved — no one can buy them. The same happens to an order
   an admin creates and never requests payment for. The 30-minute payment
   window doesn't see these orders, because it looks at payments, and
   they have none.
2. **A deadline for orders without payment.** An order still waiting for
   payment, with no payment started, some time after checkout is ended by
   the system: its reservations are released and it leaves
   `PendingPayment`, visible to the customer, the admin, the timeline and
   the real-time screens like any other status change.
3. **Repeating the checkout still works until then.** Within the
   deadline, repeating the checkout with the same `Idempotency-Key` starts
   the missing payment, as today; after it, the customer checks out again.
4. **Reservations get a lifetime too**, as a safety net under the order's
   own deadline: a reservation that is neither consumed nor released long
   after it was made is expired by Inventory, with its units back in
   stock — the `ExpireReservationUseCase` that exists today but nothing
   runs.
5. **Measured and audited**: how many orders expire without payment, and
   how many reservations the safety net had to expire (which should be
   none — a non-zero count means something upstream broke).

## Why

- **Blocker 7 of the production-readiness review:** stock stuck in
  reservations that will never be paid is invisible (the product looks
  sold out) and only fixable by hand.
- **Checkout is a sequence, not a transaction** (`CLAUDE.md`, Operations
  across modules): a failure between saving the order and starting the
  payment is expected to happen; the design already makes it repairable
  by repeating the request, but nothing repairs it when no one does.
- **Inventory already models expiry** (`InventoryReservation.ExpiresAt`,
  `Expire`, `ExpireReservationUseCase`) but never sets or runs it.

## Out of scope

- Reminding the customer by e-mail that an order is waiting for payment.
- Changing the 30-minute payment window for orders that do have a payment.

## Decisions (resolved with the user)

1. **30 minutes, then `PaymentFailed`.** An order with no payment started
   30 minutes after checkout (the same as the payment window) ends as
   `PaymentFailed` with reason `payment_not_started` and its stock is
   released. `Cancelled` stays for someone deciding to cancel, as in the
   Stripe spec.
2. **Reservations live at most 2 hours.** Well above the 30-minute order
   and payment deadlines, so the safety net never races them and only
   catches what slipped through; each reservation it expires is logged as
   a warning and counted.
