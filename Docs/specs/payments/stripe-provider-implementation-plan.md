# Implementation plan — Stripe as payment provider

Implements `Docs/specs/payments/stripe-provider.md`. Builds on the
outbox/inbox and events of `Docs/specs/events/async-messaging-implementation-plan.md`
(the webhook publishes through the outbox, the automatic cancellations
travel as events) and on the metrics conventions of the Observability
plan, so it is implemented last in V3. Staged as usual: each stage leaves
`dotnet build`, `dotnet test` (all three projects) and
`dotnet format OrderCore.sln --verify-no-changes` passing and ends in its
own commit(s).

All choices below follow the spec's nine decisions or an existing
project convention.

## Shape

```text
Storefront                OrderCore                               Stripe
──────────                ─────────                               ──────
GET payments/methods ───► Card (+ publishable key)
POST orders/checkout ───► reserve stock, save PendingPayment,
                          create PaymentIntent (manual capture) ──► requires_payment_method
                 ◄─────── 202 + payment.nextAction { clientSecret }
Payment Element confirms card (3-D Secure in place) ────────────────► requires_capture
                          POST payments/webhooks/stripe ◄────────── payment_intent.amount_capturable_updated
                          verify signature, dedupe event,
                          Payment → Authorized, outbox PaymentAuthorized
                          ... Orders confirms (as today, via RabbitMQ)
admin "enviar" ─────────► capture ─────────────────────────────────► succeeded
admin/customer cancel ──► void (cancel intent) or refund ──────────► canceled / refunded
```

## Stage 1 — Provider selection, available methods, capabilities

- `Payments:Stripe` options (`SecretKey`, `PublishableKey`,
  `WebhookSecret`), from the environment/user-secrets only. The provider
  registered is Stripe when `SecretKey` is set, the fake otherwise
  (decision 3); the choice is logged at startup.
- `IPaymentProvider` gains what the fake never needed:
  - `Name` and `SupportedMethods` (Stripe: `Card`; fake: `Card`, `Pix`);
  - `AuthorizeAsync` may answer **"needs the buyer"** — the provider
    reference plus a client secret — besides approved/declined (the fake
    keeps answering at once);
  - `CancelAsync` for a payment still waiting for the buyer (cancels the
    intent) and `GetStateAsync` (the provider's view of a payment, for
    reconciliation).
- `GET payments/methods` (anonymous): available methods, the provider
  and, for Stripe, the publishable key. Orders asks `IPaymentGateway`
  for the available methods and checkout refuses an unavailable one with
  `400 payment_method_unavailable` (decision 7).

Tests: provider selection by configuration; checkout with Pix under the
Stripe configuration is refused; the methods endpoint is anonymous.

## Stage 2 — The Stripe provider

- `Modules/Payments/Infrastructure/Providers/Stripe/StripePaymentProvider`
  with the official `Stripe.net` SDK:
  - authorize: create a PaymentIntent — amount in the smallest currency
    unit, `capture_method=manual`, card only, metadata with order and
    payment ids — and return its id and client secret;
  - capture, cancel (void), refund (amount), retrieve;
  - every call that creates or changes money sends an idempotency key
    derived from the payment (and from the refund id for refunds), so a
    retried call never charges twice;
  - the SDK's network retries on, a timeout per call; Stripe errors map
    to the provider results' failure codes (card declines keep Stripe's
    decline code), network failures to exceptions as the fake's
    `Timeout`/`Unavailable` modes already do.
- The client secret is not stored: a replayed checkout (same
  `Idempotency-Key`) retrieves it from Stripe again.

Tests: against **`stripe-mock`** (Stripe's official API mock, as a
Testcontainers container) — request shapes, idempotency keys, error
mapping — so no Stripe account or network is needed in CI.

## Stage 3 — Checkout with confirmation in the browser

- `CreatePaymentUseCase`: when the provider needs the buyer, the payment
  stays `Processing` with its provider reference, nothing is published
  yet, and the result carries the next action.
- Orders: `IPaymentGateway.RequestPaymentAsync` returns the next action;
  `CheckoutUseCase` and the checkout response gain
  `payment.nextAction = { type: "confirm_card", clientSecret }` (null with
  the fake). The order stays `PendingPayment`.
- The cancellation settlement learns the new case: a `Processing`
  payment whose provider can cancel it (Stripe, waiting for the buyer)
  is cancelled instead of answering `payment_in_progress`.

Tests (fake and a stubbed "needs the buyer" provider): the checkout
response carries the next action; replaying the checkout returns it
again; cancelling an order waiting for the buyer cancels the payment.

## Stage 4 — Webhooks

- `POST /api/payments/webhooks/stripe` — the one anonymous write
  endpoint: authenticated by Stripe's signature instead of a token (it
  is classified `[AllowAnonymous]` with that reason in its doc comment).
  Reads the raw body, verifies `Stripe-Signature` with the webhook secret
  (and its timestamp tolerance), answers 400 to a bad signature, 200
  once handled, 500 to let Stripe retry on a failure.
- Deduplicated by Stripe's event id in Payments' inbox (same mechanism
  as the Eventos inbox, consumer `stripe-webhooks`). Out-of-order events
  are resolved against the payment's current state (a late
  "authorized" for a voided payment is ignored).
- Events handled (`StripeWebhookHandler`, replacing the unused
  `PaymentWebhookHandler`):
  - `payment_intent.amount_capturable_updated` → `Authorized`, publish
    `PaymentAuthorized`;
  - `payment_intent.payment_failed` → record the decline (reason,
    time) on the payment; it stays `Processing` for the buyer to retry
    (decision 9);
  - `payment_intent.succeeded` → confirms a capture;
  - `payment_intent.canceled` → `Voided`; when Stripe cancelled it
    because the authorization expired, publish the new
    **`PaymentAuthorizationExpired`** event (decision 6);
  - `charge.refund.updated` → the refund `Completed`/`Failed`;
  - `charge.dispute.created` → recorded on the payment (and audited).
- `Payment` gains the last decline (reason, at) and a dispute flag;
  migration `AddStripePaymentFields`.

Tests: signatures computed with a test secret (valid, tampered, stale);
each event's effect; a duplicate event does nothing; an out-of-order
one is ignored.

## Stage 5 — Payment window and authorization expiry

- **Payment window (decision 8):** a Payments background job finds
  payments still `Processing` 30 minutes after they were created,
  cancels them at the provider and fails them with the last decline
  reason or `payment_window_expired`, publishing `PaymentFailed` — so
  Orders ends the order as `PaymentFailed` and releases its stock through
  the existing path.
- **Authorization expiry (decision 6):** the payment knows when its
  authorization expires (Stripe gives the capture deadline); the admin
  order list/detail and the dashboard show authorizations expiring
  within two days. Orders consumes `PaymentAuthorizationExpired` and
  cancels the order as the system (reason `authorization_expired`), which
  returns its stock — the settlement finds the payment already voided
  and does nothing more.

Tests (clock controlled with `FakeTimeProvider`): a payment left
unconfirmed for 30 minutes ends the order `PaymentFailed` with its stock
released; the expiry event cancels a confirmed order and returns its
stock; the admin sees the expiring flag.

## Stage 6 — Reconciliation

- `ReconcilePaymentUseCase`: asks the provider for the payment's state
  and applies it through the same transitions the webhook uses (one code
  path for "the provider says X"), recording whether anything changed.
- A background job every 15 minutes reconciles payments `Processing` for
  more than 5 minutes and `Authorized` ones whose provider state may have
  moved (e.g. expired without a webhook).
- `POST payments/{id}/reconcile` (admin) re-checks one payment and
  answers what changed. Audit action `PaymentReconciled`; metric of
  divergences found.

Tests: a lost "authorized" webhook is recovered by reconciliation; a
payment already in sync is left untouched.

## Stage 7 — Local development

- `docker-compose.yml`: a `stripe-cli` service under a `stripe` profile
  (only started with `--profile stripe`, i.e. when the keys exist)
  running `stripe listen --forward-to api:8080/api/payments/webhooks/stripe`;
  its webhook signing secret is the one the API uses locally.
- `.env.example`: `STRIPE_SECRET_KEY`, `STRIPE_PUBLISHABLE_KEY`,
  `STRIPE_WEBHOOK_SECRET` (empty — the fake provider is used).
- Stripe's test cards documented (approved, declined, 3-D Secure).

## Stage 8 — Docs

- `06-payments.md` (Stripe provider, webhook handler, reconciliation,
  jobs, new fields/events), `05-orders.md` (next action, expiry
  consumer), overview.
- `ORDERCORE_CONTEXT.md` sections 14, 15, 28 and 29: what exists now.
- `CLAUDE.md`: provider capabilities instead of provider checks in use
  cases; webhooks are anonymous but signature-verified and deduplicated;
  never store card data or client secrets.
- `README.md`: running with Stripe test mode, the methods endpoint, the
  checkout's `nextAction`, the Payment Element flow for the frontend.
- Execution notes appended to this plan.

## Commits

1. `feat(payments): provider selection, capabilities and available payment methods`
2. `feat(payments): Stripe provider with manual capture and idempotency keys`
3. `feat(orders,payments): checkout returns the card confirmation step`
4. `feat(payments): verified, deduplicated Stripe webhooks`
5. `feat(payments,orders): payment window and authorization expiry`
6. `feat(payments): reconciliation with the provider`
7. `chore: Stripe CLI in docker compose`
8. `docs: ...`
