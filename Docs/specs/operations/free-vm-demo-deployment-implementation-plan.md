# Implementation plan — the demo on a free VM

Implements `Docs/specs/operations/free-vm-demo-deployment.md`. Branch
`v6/free-vm-demo` (on top of `v6/aws-demo`); `dotnet` code is untouched, so the
checks are the shell, YAML and workflow linters CI already runs.

- `render-env.sh`: parameters come from the file named by `PARAMS_FILE`
  (`name=value` lines) when set, else from SSM; the public address falls back to
  `checkip.amazonaws.com` on such a server instead of the EC2 metadata.
- `deploy.sh`: uses `/opt/ordercore/params.env` when it exists.
- `create-parameters.sh`: with `PARAMS_FILE` it reads and writes that file
  (mode 600) instead of SSM, and also asks for the backup bucket and the public IP.
- `bootstrap-ubuntu.sh`: Docker, AWS CLI, `iptables` rules for 80/443 (saved),
  and `deploy.sh` and `create-parameters.sh` under `/opt/ordercore`.
- `backup-bucket.yaml`: the bucket (private, encrypted, 30-day lifecycle,
  retained) and the IAM user `ordercore-demo-backup` limited to it.
- CI: `deploy-demo-ssh` (gated on `vars.DEMO_SSH_HOST`, secret `DEMO_SSH_KEY`);
  the `deploy-files` job also lints `backup-bucket.yaml`; `shellcheck` already
  covers the scripts.
- Runbook `Docs/operations/free-vm-demo.md`; pointers from `aws-demo.md`, the
  README and `.claude/CLAUDE.md`.

Verified here: `create-parameters.sh` → `render-env.sh` end to end in file mode
with stubs, shellcheck, cfn-lint, actionlint. Not verifiable here: the Oracle VM,
`bootstrap-ubuntu.sh` on a real Ubuntu, the SSH deploy job, S3 uploads.
