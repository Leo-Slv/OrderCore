# Implementation plan — Authentication and customer account

Implements `Docs/specs/identity/authentication-and-account.md`. Adds one
new technical module (`Identity`, same kind as `AuditLogs`) and changes
Customers, Orders, Catalog and every controller's authorization. Staged
like the storefront MVP: each stage leaves `dotnet build`, `dotnet test`
(all three projects) and `dotnet format OrderCore.sln --verify-no-changes`
passing and ends in its own commit(s).

## Decisions made while planning (not asked; conventional defaults)

- **Tokens travel in the JSON body**, not in cookies set by the API. The
  Next.js app is expected to act as a BFF and keep the refresh token in
  an httpOnly cookie on its own origin; the API stays cookie-free, which
  avoids cross-site cookie/CSRF handling between two origins.
- **Access token** 15 minutes, **refresh token** 14 days, both from
  configuration (`Jwt:AccessTokenMinutes`, `Jwt:RefreshTokenDays`).
- **Password policy**: 8–128 characters, at least one letter and one
  digit. Hashing uses ASP.NET Core's `PasswordHasher<T>` (PBKDF2, salted,
  versioned — part of the shared framework, no extra package).
- **Refresh sessions are children of the `UserAccount` aggregate**, not a
  separate aggregate: rotation (revoke the current session, add its
  successor) and reuse detection (revoke the whole session family)
  become single-aggregate operations with one save, so Identity needs no
  `IUnitOfWork` (CLAUDE.md, Transactions). Expired sessions are pruned
  on each rotation so the collection stays small.
- **Sign-up owns the e-mail in `Identity`**: the account is created first
  (reserving the e-mail under a unique index), then the `Customer`, then
  the account is linked to it. If creating the customer fails, the
  account is deleted — compensation stays inside Identity's own data
  (two `DbContext`s, so this is a sequence, as in checkout).
- **Default-deny.** A fallback authorization policy requires an
  authenticated user; public endpoints opt out with `[AllowAnonymous]`.
  A test fails the build if any controller action is neither
  `[AllowAnonymous]` nor carries an explicit `[Authorize(Policy = ...)]`.
- **Existing customer rows** (development/test data only — there is no
  production) keep existing in Customers but have no account; their old
  client-supplied "hash" could never be verified anyway. Local databases
  should be recreated; this is noted in the README.
- **Customer self-cancel stays out** of this feature: cancelling a paid
  order must settle the payment, which is backoffice decision 2 — both
  cancel endpoints become admin-only until then.

## Stage 1 — Shared: current user and 401/403 as ProblemDetails

- `Shared/Application/Abstractions/ICurrentUser` — `UserId?`, `CustomerId?`,
  `Role?`, `IsAuthenticated`, `IsAdmin`. Implemented by
  `Shared/Presentation/Authentication/HttpContextCurrentUser` over the
  request's claims; outside a request (background services) it is
  "nobody".
- `Shared/Application/Exceptions/UnauthorizedException` (`Code`, e.g.
  `invalid_credentials`, `invalid_refresh_token`) → **401** in
  `ApiExceptionHandler`.
- `app.UseStatusCodePages()` backed by the ProblemDetails service, so the
  framework's own 401/403 (missing token, wrong role) also answer with a
  `ProblemDetails` body carrying `code` `unauthenticated` / `forbidden`.
- `AuditLogService` takes `ICurrentUser` and records
  `userId ?? currentUser.UserId`: all 16 existing audit call sites get an
  actor without being edited; the outbox background service records
  none (system).

Tests: unit tests for the handler mapping and `AuditLogService` actor
fallback.

## Stage 2 — Identity module: Domain, Application, Infrastructure

`Modules/Identity/{Domain,Application,Infrastructure,Presentation}` +
`IdentityDependencyInjection.AddIdentityModule(configuration)`.

**Domain**
- `UserAccount` (aggregate root): `Email`, `NormalizedEmail`,
  `PasswordHash`, `Role` (`UserRole`: `Customer`, `Admin`), `CustomerId?`,
  `Active`, `CreatedAt`, `LastSignedInAt?`, `Sessions`.
  `CreateCustomer(email, passwordHash, now)`, `CreateAdmin(...)`,
  `LinkCustomer(customerId)` (once, customers only),
  `StartSession(tokenHash, expiresAt, now)`,
  `RotateSession(presentedHash, newHash, expiresAt, now)` — returns the
  new session, or on a revoked/unknown-in-family hash revokes the whole
  family and throws `DomainRuleViolationException("refresh_token_reused")`,
  `RevokeSession(hash, now)`, `RecordSignIn(now)`, `Deactivate()`.
- `RefreshSession` (child): `TokenHash`, `FamilyId`, `CreatedAt`,
  `ExpiresAt`, `RevokedAt?`, `ReplacedBySessionId?`.
- Domain events: `UserAccountCreated`, `RefreshTokenReuseDetected`.

**Application**
- Contracts: `IUserAccountRepository` (`GetByIdAsync`,
  `GetByNormalizedEmailAsync`, `GetBySessionTokenHashAsync`,
  `AnyAdminAsync`, `AddAsync`, `Remove`, `SaveChangesAsync`),
  `IPasswordHasher` (`Hash`, `Verify`), `IAccessTokenIssuer`
  (`Issue(UserAccount) → AccessToken(value, expiresAt)`),
  `IRefreshTokenGenerator` (random token + its SHA-256 hash),
  `ICustomerRegistry` (→ Customers: register a customer, return its id).
- `PasswordPolicy` (Application/Validation) → `validation_error` details.
- Use cases: `SignUpCustomerUseCase`, `SignInUseCase`
  (`invalid_credentials` for unknown e-mail, wrong password or inactive
  account alike), `RefreshSessionUseCase`, `SignOutUseCase`,
  `SeedAdminUseCase` (no-op when an admin exists).
- DTOs: `SignUpCommand`, `SignInCommand`, `AuthTokens` (access token,
  its expiry, refresh token, its expiry, role).

**Infrastructure**
- `IdentityDbContext` (tables `user_accounts`, `refresh_sessions`; unique
  index on `NormalizedEmail`, index on `TokenHash`), persistence models,
  `UserAccountMapper` (reusing `ChildCollectionReconciler`,
  `ApplyChanges` copies `Version`, child key `ValueGeneratedNever`),
  `EfUserAccountRepository`, migration `InitialIdentitySchema`.
- `AspNetPasswordHasher` (wraps `PasswordHasher<UserAccount>`),
  `JwtAccessTokenIssuer` (claims `sub`, `email`, `role`, `customer_id`;
  signed with `Jwt:SigningKey`), `RandomRefreshTokenGenerator`.
- `Adapters/CustomerRegistryAdapter` → Customers'
  `RegisterCustomerUseCase` (Application layer only).
- `JwtOptions` bound and validated on start: missing or < 32-byte
  signing key fails startup with a clear message.

Tests: unit tests for `UserAccount` (rotation, reuse revokes the family,
expired session, link once) and every use case with fakes; integration
tests for `EfUserAccountRepository` (unique e-mail, lookup by token
hash, session add on a loaded account — the child-insert trap).
`ModuleBoundaryTests` gains `Identity` in its module list and the
"Application doesn't depend on another module's Domain/Infrastructure"
rule for Identity.

## Stage 3 — Customers: no password, account-facing use cases

- `Customer` loses `PasswordHash` (`Create(name, email, now)`,
  `Rehydrate`, persistence model, mapper, configuration) — migration
  `RemoveCustomerPasswordHash`. `RegisterCustomerCommand`/`Request` lose
  it too.
- `POST customers` (public registration) is **removed**: sign-up goes
  through Identity only, so no customer can exist without an account
  from now on.
- New domain method `Customer.UpdateAddress(addressId, label,
  recipientName, phone, Address)` (only contact info was editable).
- Use cases: `UpdateCustomerAddressUseCase`,
  `RemoveCustomerAddressUseCase`, `SetDefaultAddressUseCase`
  (shipping/billing). `UpdateCustomerProfileUseCase` already exists.
- Admin additions needed by the backoffice later are **not** added here.

Tests: domain + use-case unit tests; repository test for removing an
address from a loaded customer.

## Stage 4 — Authentication wiring and endpoint classification

- Package `Microsoft.AspNetCore.Authentication.JwtBearer`; `AddAuthentication().AddJwtBearer(...)`
  validating issuer, audience, lifetime and signing key from `Jwt:*`;
  `AddAuthorization` with policies `Customer` (role `Customer` and a
  `customer_id` claim) and `Admin`, plus the default-deny fallback
  policy; `UseAuthentication`/`UseAuthorization` in `Program.cs`.
- `Identity/Presentation/Controllers/AuthController` (`auth`):
  `POST auth/sign-up`, `POST auth/sign-in`, `POST auth/refresh`,
  `POST auth/sign-out` — all `[AllowAnonymous]` except sign-out
  (authenticated). Responses: `AuthTokensResponse`.
- `AdminSeedHostedService` runs `SeedAdminUseCase` on start when
  `IdentitySeed:AdminEmail`/`IdentitySeed:AdminPassword` are set; logs a
  warning when no admin exists and none is configured.
- Classification of every existing action:

| Access | Endpoints |
|---|---|
| Public | `GET /`, `/health`, OpenAPI/Scalar (Development), `GET catalog/products`, `GET catalog/products/by-slug/{slug}`, `GET catalog/categories`, `POST orders/cart/quote`, `auth/sign-up|sign-in|refresh` |
| Customer | checkout, `/me` endpoints (Stage 5), `GET orders/{id}` and `…/status-history` (own orders; admins see all) |
| Admin | everything else: catalog writes and `GET catalog/products/{id}`, inventory, payments, audit logs, `GET customers/{id}` and `…/addresses`, `GET orders/customers/{customerId}`, the step-by-step order endpoints, cancel |

- Every protected action documents `401`/`403` (`ProblemDetails`).
- OpenAPI: a document transformer adds the `Bearer` security scheme and
  an operation transformer marks operations that require it, so Scalar
  can send the token.
- Config: `appsettings.json` gets `Jwt:Issuer`/`Audience`/lifetimes (no
  key). Local development reads `Jwt__SigningKey` and the admin seed
  from environment/user-secrets; `docker-compose.yml` passes them through
  from a git-ignored `.env`, with a committed `.env.example`.

## Stage 5 — "Me" endpoints and ownership

- `CustomersController`: `GET/PUT customers/me`,
  `GET/POST customers/me/addresses`, `PUT/DELETE customers/me/addresses/{addressId}`,
  `POST customers/me/addresses/{addressId}/default-shipping|default-billing`.
  The customer id always comes from `ICurrentUser`, never from the route.
- `OrdersController`: `GET orders/me` (paged history), checkout takes the
  customer from the token (`CheckoutRequest.CustomerId` removed).
- Ownership is a use-case rule, not a controller check:
  `GetOrderDetailsUseCase`/`GetOrderStatusHistoryUseCase` take the
  requesting customer id (null for admins) and answer `order_not_found`
  for someone else's order.
- Catalog: `ListProductsUseCase` gains an "include unpublished" flag the
  controller sets only for admins; everyone else sees published, active
  products only, whatever the query string says.

## Stage 6 — HTTP-level tests

- `AuthFlowTests`: sign-up → sign-in → `customers/me` → refresh →
  replaying the old refresh token revokes the session (the new one stops
  working too) → sign-out → refresh rejected; weak password and duplicate
  e-mail rejected with their codes.
- `EndpointAuthorizationTests`: reflection over all controller actions
  (every one classified); anonymous gets 401 and a customer gets 403 on a
  sample of admin endpoints; public ones answer without a token.
- `OwnershipTests`: customer B gets 404 for customer A's order and can't
  touch A's addresses; admin sees both.
- `StorefrontCheckoutTests` and `ErrorContractAndCorsTests` move to
  tokens (admin seeded through configuration in the test host).

## Stage 7 — Docs

- New `Docs/diagrams/implementation-class/08-identity.md`; index and
  `00-overview.md` gain Identity and the `Identity → Customers` contract;
  `01-shared-kernel.md` (`ICurrentUser`, `UnauthorizedException`, 401),
  `02-customers.md`, `05-orders.md`, `03-catalog.md`, `07-auditlogs.md`
  (actor) updated.
- `ORDERCORE_CONTEXT.md`: module list, section 7 contracts table, section
  32 (what is implemented), section 39 (access classes).
- `.claude/CLAUDE.md`: Identity in the module list; the default-deny rule
  (every new action must be classified, the test enforces it); "me"
  endpoints take the customer from `ICurrentUser`.
- `README.md`: how to run with `.env` (signing key, first admin), the
  auth endpoints, recreating local databases.

## Commits

One or more per stage, Conventional Commits, e.g.
`feat(shared): current user abstraction and 401/403 problem details`,
`feat(identity): user accounts, password hashing and refresh sessions`,
`refactor(customers)!: move credentials out of Customer`,
`feat(api): JWT authentication and default-deny endpoint policies`,
`feat(customers,orders): me endpoints and order ownership`,
`test: authentication, authorization and ownership over HTTP`,
`docs: document the Identity module and endpoint access rules` — plus
the spec + this plan as `docs(identity): ...` before Stage 1, and the
backoffice spec on its own.
