# The OrderCore demo on a free VM, with S3 only on AWS

Amends `Docs/specs/operations/aws-demo-deployment.md` (V6).

## What

The demo can run on a server **outside AWS** — a free VM — with the same
stack and files as the EC2 variant, and AWS used only for an S3 bucket that
keeps the daily PostgreSQL backups (and the pre-deploy ones) for 30 days.

- The server's secrets and settings live in a root-only file on the server
  instead of SSM Parameter Store; the same names, so the same scripts read them.
- CI can deploy it over SSH, the way it deploys EC2 through SSM, and only once
  the repository is configured for it.
- A one-time setup script prepares an Ubuntu server (Docker, AWS CLI, firewall).
- A small CloudFormation template creates just the bucket and a user that can
  only touch it; no secret passes through CloudFormation.
- E-mail stays on the demo inbox (Mailpit behind a password) by default; a
  development-only Resend key (no verified domain) can only reach its owner, so
  it is optional and documented as such.

## Why

An always-on EC2 instance is the only meaningful cost of the demo (~US$ 18 a
month). A free VM removes it, while S3 keeps the backups off the server — the
property that makes the demo recoverable, since the VM's disk is the only copy
of the data.

## Out of scope

- Other providers' specifics beyond the Oracle Always Free walkthrough.
- Moving the secrets of a free VM into a managed store.
- Zero-downtime deploys, a second instance, a real domain.

## Decisions (asked and answered)

1. A free VM outside AWS plus S3 for backups only (the user asked for "an S3
   only, for cost"; S3 alone cannot run the API, the database or the broker).
2. The user's Resend key is development-only: not used for the demo's mail.
3. Secrets of that server in a file on the server (decision 9 of V6, SSM, applies
   to the EC2 variant).
