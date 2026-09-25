# Stripe as payment provider (V3, "Pagamentos")

## What

Payments are processed by a real provider, Stripe (in test mode), behind
the `IPaymentProvider` abstraction that exists for exactly this
(`ORDERCORE_CONTEXT.md` sections 14 and 15), with the provider's
asynchronous answers arriving through verified webhooks (section 28).

1. **A real provider behind the abstraction.** A Stripe implementation
   of `IPaymentProvider` authorizes (holding the money without taking
   it), captures when the order ships, voids when a paid order is
   cancelled before shipping, and refunds — the same lifecycle the
   backoffice already drives through the fake provider. The fake
   provider stays for tests and for running without Stripe credentials.
2. **Card data never reaches OrderCore.** The buyer types their card into
   Stripe's own components on the storefront; OrderCore only ever holds
   Stripe's references. This keeps OrderCore out of card-data (PCI)
   scope and lets Stripe run 3-D Secure challenges when the bank asks
   for one.
3. **Checkout hands the payment to the browser.** Because the card is
   confirmed in the browser, checkout no longer finishes the
   authorization itself: it creates the payment at Stripe and returns
   what the storefront needs to confirm it; the order stays
   `PendingPayment` until Stripe reports the outcome (decisions 1, 8 and
   9).
4. **Webhooks.** Stripe's notifications (authorization succeeded or
   failed, payment canceled, refund updated, dispute opened...) reach a
   public endpoint that verifies Stripe's signature, ignores an event it
   has already handled (webhooks can arrive twice or out of order),
   updates the payment and publishes the payment's integration events
   through the outbox — so Orders reacts exactly as it does today.
5. **Pix.** The storefront already offers Pix; with Stripe it is not
   available (decision 2), so the storefront asks which methods are
   available before offering them.
6. **Nothing charged twice, nothing held forever.** Idempotency keys are
   sent to Stripe on every call that creates or changes money, so a
   retried request never charges twice; an authorization that is never
   captured or voided is not left hanging silently (see reconciliation).
7. **Secrets stay secret.** Stripe's keys and webhook signing secret come
   from the environment/user-secrets like the JWT key, never from the
   repository; running locally with real Stripe test mode is documented,
   including receiving webhooks on a developer machine.

## Why

- **It is V3 of the frontend plan** ("Pagamentos"; the plan's payment
  screen shows "Provider: Stripe", "Transaction: ch_…"), and the project
  context always planned a Stripe provider after the fake one.
- **It proves the abstraction.** `IPaymentProvider` was introduced
  because a second, real implementation was expected; until one exists,
  the abstraction is only a promise.
- **Real providers are asynchronous.** 3-D Secure, delayed bank
  answers, disputes and refunds settled later only exist with a real
  provider; the webhook path, the idempotency and (later) reconciliation
  are what make PayCore (V4) credible.
- **The capture-on-ship and void-on-cancel decisions** of the backoffice
  map directly to Stripe's manual capture, which is how a real shop
  holds money until it ships.

## Out of scope

- Moving Payments into PayCore (V4) — the webhook endpoint lives in
  OrderCore's Payments module for now.
- Saved cards / Stripe customers for returning buyers
  (`CustomerPaymentMethod` stays unused).
- Subscriptions, installments ("parcelamento"), boleto, other providers.
- Handling disputes beyond recording them on the payment.
- Going live (real money, live keys, Stripe account activation).

## Decisions (resolved with the user)

1. **Stripe's Payment Element.** The card form is Stripe's component
   embedded in the storefront's checkout page (the buyer never leaves
   the site; 3-D Secure appears in place). Checkout creates the payment
   at Stripe and returns what the page needs to confirm it; the
   authorization arrives by webhook.
2. **Pix only on the fake provider.** With Stripe, the shop takes cards;
   Pix keeps working with the fake provider (development, tests) until
   there is a Brazilian Stripe account or another Pix provider.
3. **The provider is chosen by configuration:** the fake one when no
   Stripe keys are configured (tests, anyone cloning the project), Stripe
   when they are.
4. **Reconciliation now.** A periodic check asks Stripe about payments
   still waiting for an answer (a lost webhook) and corrects the local
   state the same way a webhook would; an admin can also ask for one
   payment to be re-checked.
5. **The Stripe CLI runs in `docker compose`** — only when the Stripe
   keys are configured — and forwards Stripe's webhooks to the local API.
6. **Authorization expiry: warn, then cancel.** The backoffice flags
   orders whose card authorization is close to expiring (from the fifth
   day); if it expires, the order is cancelled automatically and its
   stock returned — an order that can no longer be charged is never
   shipped.
7. **Available payment methods are public.** A public endpoint lists the
   methods available right now (and Stripe's publishable key, which the
   Payment Element needs); checkout with an unavailable method answers
   `400 payment_method_unavailable`.
8. **A 30-minute payment window.** An order whose payment isn't
   confirmed within 30 minutes of checkout ends as `PaymentFailed`
   (reason `payment_window_expired`, or the last decline reason if the
   buyer tried), its stock released and the payment cancelled at Stripe.
   `Cancelled` stays for someone deciding to cancel.
9. **A declined card isn't the end.** A decline keeps the order waiting
   for payment; the buyer can try another card on the same page within
   the window. Only when the window expires does the order become
   `PaymentFailed`. (With the fake provider, which answers at once, a
   decline still ends the order immediately, as today.)
