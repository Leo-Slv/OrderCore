# Implementation plan — OrderCore on Vercel

Spec: `Docs/specs/operations/vercel-hosting.md`. Branch `v6/vercel-hosting`.

- `Shared/Infrastructure/Hosting/HostingRole.cs`: `HostingRole` (All, Web,
  Worker), `HostingRoles.Read` (`Hosting:Role`, case-insensitive, unknown value
  stops startup) and `AddBackgroundWorker<T>` — registers a hosted service only
  when the role isn't Web.
- The ten `AddHostedService<T>()` calls in Identity, Inventory, Messaging,
  Notifications, Orders and Payments become `AddBackgroundWorker<T>(configuration)`.
- `RabbitMq:Tls` (`RabbitMqOptions`, `RabbitMqConnection`: `SslOption`, server
  name = host).
- `Dockerfile.vercel` (Web, `amd64`, `PORT` 8080) and `vercel.json` (the
  `worker` daemon: root `.`, `Dockerfile`, `command` with
  `--Hosting:Role=Worker`, one instance in `iad1`, 1 GiB).
- Tests: `HostingRoleTests` (parsing, registration per role).
- Checked locally: both images against the local PostgreSQL and RabbitMQ in
  Production mode — the Web copy starts without the workers, the Worker copy
  connects to the broker and declares its queues, both answer `/health/ready`.
- Docs: `Docs/operations/vercel-hosting.md`, README, `.claude/CLAUDE.md`.
