# OrderCore on Vercel (container Function + Daemon)

Amends `Docs/specs/operations/aws-demo-deployment.md` and
`free-vm-demo-deployment.md`: a third way to host the demo.

## What

- The API can be hosted on Vercel's container runtime, for the Marfim demo
  (https://marfim-kappa.vercel.app). One image build serves two roles, chosen
  by `Hosting:Role`: **Web** answers HTTP only, **Worker** runs the background
  services only; **All** (default) is today's single process.
- A `Dockerfile.vercel` at the repository root builds the Web role for Vercel's
  container Functions; a Daemon declared in `vercel.json` runs the Worker role
  from the regular `Dockerfile`.
- PostgreSQL and RabbitMQ come from external managed services; the broker
  connection can use TLS (`RabbitMq:Tls`).
- A runbook lists the services to create, the schema step and every variable.

## Why

Vercel container Functions are request-driven and scale to zero, and Daemons
can't receive HTTP: OrderCore's consumers, outbox relay, e-mail dispatcher and
jobs would stop with the Function, leaving orders stuck after payment and no
e-mail sent. Splitting by role keeps behavior correct while the Marfim
storefront and the API live on the same platform.

## Out of scope

- Real-time order tracking over SignalR in this layout (no backplane);
  Marfim doesn't use it.
- Running migrations on Vercel (manual, from a machine).
- A backplane or multi-instance workers.

## Decisions (asked and answered)

1. Split web + worker by configuration (not a single process, not another host).
2. Postgres and RabbitMQ external (Neon and CloudAMQP suggested).
