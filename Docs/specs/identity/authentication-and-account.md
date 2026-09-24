# Authentication and customer account (V2, part 1)

## What

Give the storefront real identities and give the API a way to tell a
customer from an administrator, so that the V2 screens (`/login`,
`/account`, order history) and the backoffice (`/admin`, see
`Docs/specs/backoffice/backoffice-api.md`) can be built safely.

1. **Sign-up with a real password.** A new customer registers with name,
   e-mail and a plain password; the API validates the password against a
   minimum policy and stores only a salted hash it computes itself. The
   current `RegisterCustomerRequest.PasswordHash` (a hash supplied by the
   client) goes away.
2. **Sign-in / sign-out.** E-mail + password exchanges for a signed,
   short-lived access token that the API accepts on every protected
   endpoint. How sessions are kept alive and ended is an open decision
   (below).
3. **Roles.** Every authenticated principal is either a `Customer` or an
   `Admin`. Administrators are not customers: they don't buy, and they
   are created by an operator, not by public sign-up.
4. **"Me" instead of ids in the URL.** A signed-in customer reads and
   changes *their own* data without sending their customer id:
   profile (`GET/PUT customers/me`), addresses (list, add, edit, remove,
   choose default shipping/billing), and order history/detail/timeline.
   The storefront checkout takes the customer from the token, not from
   the request body.
5. **Ownership everywhere.** A customer can only see or act on their own
   orders, addresses and payments; asking for someone else's is
   indistinguishable from asking for something that doesn't exist (404).
6. **Endpoint protection.** Every endpoint is classified as public
   (catalog browsing, cart quote, sign-up, sign-in, health, and the
   OpenAPI docs in Development), customer-only, or admin-only, and the
   classification is visible in the OpenAPI document. The catalog listing
   hides drafts from anyone who is not an admin.
7. **Audit trail with an actor.** Audit log entries record who performed
   the action (the `userId` that is `null` everywhere today).

## Why

The MVP identifies the customer by an id sent in the request, and every
admin endpoint (create product, adjust stock, refunds, audit logs) is open
to anyone who can reach the API. That was acceptable to get the purchase
path working, but nothing in V2 can ship like that: an order history
keyed by a guessable id leaks other people's orders, and a backoffice
without roles is just an open door. It is also the precondition the
architecture context already lists (section 32: authentication,
authorization, protection of administrative endpoints) and the reason
the audit log can't yet say *who* did something.

Storing a client-computed "password hash" is also a security bug in its
own right: the hash *becomes* the password, and the server can't enforce
salting, work factor or a password policy.

## Out of scope

- E-mail verification, password reset/"forgot password" and changing
  e-mail (the domain already has `EmailVerifiedAt`; flows come later).
- Social login / external identity providers, multi-factor auth.
- Fine-grained admin permissions (every admin can do every admin thing).
- Saved payment methods (`CustomerPaymentMethod`) exposed to the
  storefront — cards stay out until a real provider (Stripe) exists.
- Rate limiting of sign-in attempts beyond what the chosen mechanism gives
  for free (tracked as a follow-up).

## Decisions (resolved with the user)

1. **OrderCore issues its own JWTs.** Signed with a key that comes from
   configuration/environment (never committed); passwords hashed with the
   framework's standard password hasher. No ASP.NET Core Identity, no
   external identity provider.
2. **A new technical `Identity` module owns credentials and roles**, the
   same kind of cross-cutting module as `AuditLogs` (not a business
   bounded context). A user account has an e-mail, a password hash, a
   role (`Customer`/`Admin`) and, for customers, the id of the matching
   `Customer`. `Customer` stops holding a password: it keeps the profile,
   `Identity` keeps the credentials. Admin accounts have no `Customer`.
   This is a new module, so the architecture docs and diagrams gain it in
   the same change.
3. **Short access token + rotating refresh token.** Refresh tokens are
   stored only as hashes, rotate on every use, and a reused (already
   rotated) refresh token revokes that session. Signing out revokes the
   refresh token.
4. **The first administrator is seeded at startup from configuration**
   (e-mail and password from environment variables, never from a
   committed `appsettings`): created only if no admin exists yet, so the
   seed is idempotent and does nothing once an admin is there.
