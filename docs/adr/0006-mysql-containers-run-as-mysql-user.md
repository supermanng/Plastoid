# ADR 0006: MySQL containers run as the `mysql` user

**Status:** Accepted (2026-11-21)

## Context

The official `mysql:8.4` image's entrypoint runs as root and re-executes itself through `gosu mysql ...`.
On the development machine (Docker Desktop 27.4, Apple Silicon, Virtualization framework) `gosu` never
returns: it spins at ~100 % CPU before `mysqld` is launched, so the container logs stop at
"Switching to dedicated user 'mysql'" and the Testcontainers wait strategy times out. Running `gosu mysql id`
on its own reproduces the hang, with or without the default seccomp profile, so it is a Go-runtime/VM
interaction and not a MySQL problem. A native arm64 `mysqld --initialize-insecure` completes in ~20 s.

A second trap on the same machine: the `mysql:8.4` tag had been pulled as `linux/amd64`, which runs
`mysqld` under QEMU and also never finishes initialising. `docker pull --platform linux/arm64 mysql:8.4`
fixed that half.

## Decision

Start MySQL containers with `user: mysql` (compose) and `WithCreateParameterModifier(p => p.User = "mysql")`
(Testcontainers). The entrypoint detects a non-root uid and skips `gosu`; `/var/lib/mysql` is already owned by
`mysql` inside the image, so initialisation and named volumes keep working.

## Consequences

- Dev and test MySQL containers start in ~10 s regardless of the `gosu` issue.
- Testcontainers' default MySQL readiness probe (`mysql --wait` reading a root-owned 0600 `my.cnf`) cannot run as
  the mysql user and retries forever, so the test factory waits for the server's "ready for connections … port: 3306"
  log line instead. `MySqlContainer.ExecScriptAsync` is likewise unavailable; tests use MySqlConnector directly.
- Anything that relied on the root-stage of the entrypoint (custom `--datadir` outside `/var/lib/mysql`,
  `MYSQL_ROOT_HOST` tweaks) must be re-checked; nothing in this repo does.
- CI (GitHub-hosted Linux runners) does not exhibit the hang; the setting is harmless there.
