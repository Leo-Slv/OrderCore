# Password recovery and transactional e-mail (V5, part 1)

## What

OrderCore can send e-mail, and uses it so that people can recover their
accounts, prove their address and follow their orders.

1. **Sending e-mail.** OrderCore sends e-mail through a provider behind an
   abstraction (the way payments sit behind `IPaymentProvider`), chosen by
   configuration (decision 1). Messages are sent reliably: a request that
   causes an e-mail doesn't fail or wait because the provider is slow or
   down, and a message isn't lost if sending fails once. Development and
   tests never send real e-mail.
2. **"Forgot my password"** — for customers and admins. Someone asks for a
   reset with their e-mail address; if an account exists, it receives a
   link to the storefront's reset page that works once and for a limited
   time (decision 2). The answer is the same whether the address has an
   account or not, so the form can't be used to discover who has one.
   Asking again replaces the previous request.
3. **Choosing a new password.** With a valid, unused, unexpired reset, the
   person sets a new password (same rules as sign-up). Every existing
   session of the account ends, so whoever had the old password is signed
   out everywhere, and the account's lockout (V4) is cleared.
4. **Changing the password while signed in**, by confirming the current
   one; the other sessions end, the current one stays.
5. **Confirming the e-mail address.** A new customer account starts with
   an unconfirmed address and receives a confirmation link; the customer
   can ask for it again. Until confirmed, the customer can browse, fill a
   cart and save addresses, but can't check out (decision 4). Accounts
   that exist before this feature count as confirmed.
6. **E-mails about the order.** The customer receives an e-mail when their
   order is confirmed, when it ships (with the carrier and tracking
   details when there are any) and when it is cancelled or its payment
   fails — driven by the integration events Orders already publishes, so
   no use case sends e-mail itself.
7. **In Portuguese** (decision 5), from templates kept in the repository.
8. **Audited and observable** like every other account event (reset
   requested and completed, password changed, e-mail confirmed) and every
   message sent or failed — with no e-mail address, token or link in logs,
   traces or metrics.

## Why

- **Blocker 6 of the production-readiness review:** without it, a customer
  who forgets their password loses their account and order history for
  good, and an admin who forgets theirs locks the store's backoffice.
- **E-mail is the missing capability** behind several gaps from the same
  review (order notifications, address verification); it is built here
  once, for all of them.
- **A confirmed address is what makes recovery safe:** a reset link sent
  to an address the customer never proved they own could hand the account
  to whoever typed it at sign-up.
- **The identity spec deferred it** (`authentication-and-account.md`, out
  of scope: e-mail verification, password reset, password change).

## Out of scope

- Social sign-in, two-factor authentication, passwordless links.
- Marketing e-mail, newsletters, unsubscribe management.
- Other languages, per-customer language preference.
- Admin-initiated resets from the backoffice.
- A reminder that an order is still waiting for payment.

## Decisions (resolved with the user)

1. **Resend in production, Mailpit locally.** E-mail goes out through
   Resend's API when its key is configured; without it, OrderCore sends
   through SMTP to the Mailpit service of `docker compose`, which shows
   every message in a local web inbox — so a developer can click a reset
   link — and which the integration tests also read. The key comes from
   the environment/user-secrets, never the repository.
2. **A link to the storefront, valid for 30 minutes.** The reset e-mail
   links to the storefront's reset page (its address is configuration)
   with a single-use token; the page sends the token and the new password
   to the API. The confirmation e-mail works the same way.
3. **Scope:** password reset for customers and admins, password change
   while signed in, e-mail confirmation at sign-up and the order e-mails
   are all part of V5 part 1.
4. **An unconfirmed customer can't check out.** Checkout answers
   `403 email_not_confirmed` (the storefront offers to resend the link);
   everything else works. Existing accounts are treated as confirmed.
5. **Portuguese (pt-BR) only.**
