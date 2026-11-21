# Plastoids backend

Sponsored-learning platform for Nigeria. Blueprint: [docs/Plastoids_Blueprint.md](docs/Plastoids_Blueprint.md). Build spec and sprint prompts: [docs/Build_Prompt.md](docs/Build_Prompt.md). Decisions: [docs/adr](docs/adr).

**Stack:** .NET 9 · ASP.NET Core Minimal APIs · MediatR · EF Core + MySQL 8.4 (Pomelo) · Apache Kafka (outbox/inbox) · Redis · Quartz · S3-compatible storage · Serilog + OpenTelemetry.

## Quick start

```bash
make up          # MySQL, Kafka, Redis, MinIO, Mailpit, Kafka UI (http://localhost:8080)
make migrate     # apply EF migrations
make run         # API on http://localhost:5080 (Development: migrates on startup, diagnostics enabled)
make test-unit   # unit + architecture tests
make test-integration   # Testcontainers: real MySQL, Kafka, Redis
```

Integration tests start MySQL in a container by default. To use an existing MySQL server instead (the tests create and drop a
throw-away `plastoids_test_<id>` database, so the account needs CREATE/DROP DATABASE):

```bash
PLASTOIDS_TEST_MYSQL="Server=host;Port=3306;User=user;Password=***" make test-integration
```

Connection details come from `appsettings.json`; override locally with `dotnet user-secrets` or environment variables
(`ConnectionStrings__Plastoids`, `ConnectionStrings__Redis`, `Kafka__BootstrapServers`). Never commit credentials.
For a MySQL 8.0 server also set `MySql:ServerVersion` (e.g. `8.0.45-mysql`) so Pomelo generates matching SQL.

MySQL containers (compose and Testcontainers) run as the `mysql` user because the image's `gosu` step hangs on some
Docker Desktop builds; see `docs/adr/0006`. If a MySQL container ever sticks at "Switching to dedicated user", also check
`docker image inspect mysql:8.4 --format '{{.Architecture}}'` is your host's architecture.

## Layout

```
src/Plastoids.Api             host, CLI (`migrate`), health, OpenAPI
src/Plastoids.SharedKernel    Result, Money, Entity, IClock, tenancy, CQRS markers, event contracts
src/Plastoids.Infrastructure  MySQL conventions, unit of work, outbox/inbox, Kafka, Quartz, behaviours
src/Modules/<Module>          Domain · Persistence · Contracts · Features/<UseCase> · Consumers · Jobs
tests/                        SharedKernel.Tests · ArchitectureTests · IntegrationTests
```

## Adding a slice

Create `Features/<UseCase>/` with `<UseCase>Command.cs` (or `Query`), `<UseCase>Validator.cs`, `<UseCase>Handler.cs`, `<UseCase>Endpoint.cs`. Commands run in one MySQL transaction; raise integration events with `IOutbox.Enqueue`. Map the endpoint in the module's `MapEndpoints`.

## Adding a migration

```bash
dotnet ef migrations add <Name> --project src/Modules/<Module> --startup-project src/Plastoids.Api --context <Module>DbContext --output-dir Persistence/Migrations
```
