# Implementation plan — password recovery and transactional e-mail (V5, part 1)

Implements `Docs/specs/identity/password-recovery.md`. Staged as usual:
each stage goes through a `v5/<topic>` branch whose CI run must be green
before it is fast-forwarded into `master`, and leaves `dotnet build`,
`dotnet test` (all three projects) and `dotnet format --verify-no-changes`
passing.

## Shape

```text
Identity (forgot / confirm)        Orders' integration events
      │ IAccountEmails (Identity contract)      │ orders.order-confirmed/-shipped/-cancelled/-payment-failed
      ▼                                          ▼  queue notifications.order-emails (inbox in NotificationsDbContext)
Notifications ── QueueEmailUseCase ──► notification_emails (Pending: to, template, rendered subject/body)
                                             │  EmailDispatcherBackgroundService (every few seconds)
                                             ▼
                                   IEmailSender ─┬─ ResendEmailSender   (Notifications:Resend:ApiKey set)
                                                 └─ SmtpEmailSender     (otherwise: Mailpit in docker compose)
                                             │ Sent / Failed after 5 attempts → body erased, recipient masked
```

## Stage 1 — The Notifications module and sending e-mail

- **Module skeleton** `Modules/Notifications` (technical, like AuditLogs
  and Messaging), `NotificationsDependencyInjection.AddNotificationsModule`,
  its own `NotificationsDbContext` (`AddDatabaseMigrations` order 90),
  architecture tests extended to the new module.
- **Domain:** `EmailMessage` aggregate — recipient, template, subject,
  HTML and text bodies, status (`Pending`, `Sent`, `Failed`), attempts,
  next attempt time, last error, sent/failed time — with `MarkSent`,
  `RecordFailure(error, nextAttempt)` / give up after 5 attempts, and
  `EraseContent()` (bodies emptied, recipient reduced to a masked form
  like `j***@example.com`), called on `Sent` and `Failed` (decision 7).
  `IEmailSender` abstraction (like `IPaymentProvider`).
- **Application:** `QueueEmailUseCase(to, template, model)` renders the
  template and saves the message — the caller never waits for the
  provider; `SendPendingEmailsUseCase` (one batch: send, record the
  outcome, erase the content when final); `NotificationsMetrics`
  (`ordercore.notifications.emails` by template and outcome).
- **Templates:** pt-BR, in the repository (`Modules/Notifications/
  Infrastructure/Templates`, embedded resources), a small `{{placeholder}}`
  renderer that HTML-encodes values; one shared layout.
- **Senders:** `ResendEmailSender` (Resend's HTTP API over a named
  `HttpClient`, with the message id as idempotency key) when
  `Notifications:Resend:ApiKey` is set; `SmtpEmailSender` (MailKit) to
  `Notifications:Smtp:Host/Port` otherwise. `Notifications:From` is the
  sender address. A provider refusal (invalid address) fails the message
  at once; a transient error retries with backoff (30 s, 2 min, 10 min,
  30 min).
- **Job:** `EmailDispatcherBackgroundService`, every 5 s, ids listed in
  one scope and each message sent in its own (as the payment jobs do).
  One instance assumed (V4).
- **docker compose:** `mailpit` service (web inbox on
  <http://localhost:8025>, SMTP 1025), the API pointed at it.
- **Startup checks:** with a Resend key, `Notifications:From` is required;
  outside Development, either Resend is configured or the SMTP host isn't
  local (`ProductionSettingsValidator`), and the storefront link settings
  of stage 2 are required.
- **Retention:** sent/failed message records older than 90 days are
  removed by the Notifications job (their content is already gone).

Tests: unit (the aggregate's retries, give-up and erasure; rendering and
encoding); integration with a shared **Mailpit** Testcontainer
(`TestMailpit`, read through its HTTP API): a queued e-mail arrives, a
failing SMTP retries and then fails with the content erased; the Resend
sender against a canned HTTP handler (request shape, idempotency key,
error mapping).

## Stage 2 — Forgot, reset and change password

- **Domain (Identity):** `UserAccount` gains an `AccountToken` child
  collection (`Purpose` = `PasswordReset` | `EmailConfirmation`,
  `TokenHash`, `ExpiresAt`, `UsedAt`): `IssueToken(purpose, hash,
  lifetime, now)` replaces an unused one of the same purpose;
  `ResetPassword(tokenHash, newHash, now)` checks it's unused and
  unexpired, marks it used, replaces the hash, ends every session and
  clears the lockout; `ChangePassword(newHash, keepSession, now)` ends the
  other sessions. Migration `AddAccountTokens`.
- **Application:** `RequestPasswordResetUseCase` (always the same answer;
  for an existing active account, issues a 30-minute token and asks
  `IAccountEmails` for the e-mail — the decoy-hash timing trick of
  sign-in applied here too), `ResetPasswordUseCase` (`400
  invalid_or_expired_token`), `ChangePasswordUseCase` (current password
  checked like sign-in, lockout included). Audited:
  `PasswordResetRequested`, `PasswordReset`, `PasswordChanged`.
- **Cross-module:** `IAccountEmails` in Identity's `Application/Contracts`,
  adapter in `Identity/Infrastructure/Adapters` calling Notifications'
  `QueueEmailUseCase`. The link is built from
  `Identity:Links:ResetPassword` (e.g. `https://shop/redefinir-senha?token={token}`).
- **Endpoints:** `POST auth/password/forgot` (anonymous, `202`),
  `POST auth/password/reset` (anonymous, `204`), `POST auth/password/change`
  (`[Authorize]`, `204`). New rate-limit policies (V4 convention):
  forgot 5 per 15 min per address, reset 10 per 15 min per address.

Tests: unit (tokens: replace, expire, single use; reset ends sessions and
clears lockout; unknown e-mail answers the same); integration: forgot →
Mailpit receives the pt-BR e-mail with a link → reset with its token →
old password refused, new accepted, old refresh token refused; a used or
expired token is refused; change password.

## Stage 3 — E-mail confirmation

- `UserAccount.EmailConfirmedAt`; migration `AddEmailConfirmation` marks
  every existing account confirmed (spec decision 4); admins are created
  confirmed.
- Sign-up issues a 24-hour confirmation token (decision 8) and queues the
  e-mail; `POST auth/email/confirm` (anonymous, token) confirms;
  `POST auth/email/confirmation` (customer) sends a new link — rate
  limited 5 per hour per customer.
- The access token carries `email_confirmed`; `ICurrentUser.EmailConfirmed`
  reads it (decision 9).
- Checkout refuses an unconfirmed customer with **`403
  email_not_confirmed`**: a new `ForbiddenException` in
  `Shared/Application/Exceptions`, mapped to 403 by `ApiExceptionHandler`
  and declared on checkout in OpenAPI.

Tests: sign-up sends the confirmation (Mailpit); checkout refused until
confirmed, then accepted after confirm + refresh; an existing account
(migrated) checks out without confirming; resend replaces the old link.

## Stage 4 — Order e-mails

- Notifications consumes `orders.order-confirmed`, `-shipped`,
  `-cancelled` and `-payment-failed` on the queue
  `notifications.order-emails` (inbox in `NotificationsDbContext`).
- The customer's address and name come through
  `ICustomerContacts` (Notifications' contract, adapter over Customers'
  `GetCustomerByIdUseCase`).
- Templates with the order number, total, and — when shipped — carrier
  and tracking link; the cancellation and payment-failure reasons shown
  as friendly text, never raw codes.
- Idempotent through the inbox; a customer without an address (deleted)
  is skipped and logged.

Tests: each event queues the right e-mail (Mailpit), a redelivery sends
nothing new, `IntegrationEventTests` keeps Notifications on Orders'
contracts only.

## Stage 5 — Docs

- New diagram `10-notifications.md`; `08-identity.md` (tokens, confirmation,
  password endpoints); `01-shared-kernel.md` (`ForbiddenException`);
  `05-orders.md` (checkout 403); overview and index.
- `ORDERCORE_CONTEXT.md` (module list, section 7 contracts, section 32
  security), `CLAUDE.md` (module list; e-mail conventions: queue, never
  send from a use case directly, never put tokens or addresses in logs),
  `README.md` (the flows for the storefront, Mailpit, Resend settings),
  runbook settings table.
- Execution notes.

## Commits

1. `feat(notifications): e-mail queue, Resend and SMTP senders`
2. `feat(identity): forgot, reset and change password`
3. `feat(identity,orders): e-mail confirmation required to check out`
4. `feat(notifications): order e-mails`
5. `docs: ...`

## Execution notes (what differed from this plan)

- **Stage 1.** Templates are a `.html` fragment plus a `.txt` whose first
  line is `Subject: …`, embedded under a stable resource name
  (`EmailTemplates/<file>`). Values are encoded with an `HtmlEncoder`
  that keeps accented letters (the BCL's default turned "Olá" into an
  entity). The dispatcher also runs the 90-day retention, once a day, in
  the same background service. Besides the plan's checks,
  `NotificationsOptionsValidator` refuses a Resend key that isn't `re_…`
  and an SMTP-less setup without Resend. A "E-mails failing" alert rule
  was added (`ordercore.notifications.emails{outcome="failed"}`), and
  every host built by `ApiDatabase` sends to the shared `TestMailpit`.
- **Stage 2.** No decoy work in "forgot password": a decoy password hash
  would make the answer for an unknown address *slower*, not equal; what
  differs is a couple of database writes, and the form is rate limited
  per address. Changing the password takes the caller's refresh token to
  know which session to keep (as sign-out does). The account tokens reuse
  `IRefreshTokenGenerator` (256 random bits, SHA-256 stored). The wrong-
  password handling of sign-in moved to `FailedSignIns`, shared with the
  password change. Templates take a ready `greeting` instead of a name,
  so an admin (no customer name) gets "Olá!".
- **Stage 3.** `ICurrentUser.EmailConfirmed` treats a token *without* the
  claim as confirmed: it was issued before this feature, to an account the
  migration marks confirmed. The confirmation link is sent last in
  sign-up, best effort: if queuing it fails, sign-up still succeeds and the
  customer asks for another. `POST auth/email/confirm` is anonymous, so it
  also got a per-address rate limit (`ConfirmEmail`, 10 per 15 min), per the
  V4 convention. Asking for a link on a confirmed address is
  `409 email_already_confirmed`; opening a link again after confirming is
  a `204`. `ApiDatabase.SignUpCustomerAsync` confirms the customer in the
  database and refreshes the session, since most tests check out; the real
  e-mail round trip has its own tests.
- **Stage 4.** A shipment with a usable http(s) tracking link uses a
  separate template (`order-shipped-tracking`, with the button); without
  one, `order-shipped`. A cancellation reason is shown only when it is the
  storefront's default customer cancellation or the payment deadline;
  anything else is free text someone typed and is replaced by a neutral
  sentence. Money is formatted by hand (`R$ 1.234,56`) so the container
  needs no culture data. The inbox row commits with the queued e-mail,
  since both live in `NotificationsDbContext`.
- Every stage was merged into `master` through its `v5/<topic>` branch
  after a green CI run; `dotnet ef` output converted to LF.
