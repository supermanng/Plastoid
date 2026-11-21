.PHONY: up down logs build test test-unit test-integration migrate seed run openapi format clean

API := src/Plastoids.Api/Plastoids.Api.csproj

up:            ## Start MySQL, Kafka, Redis, MinIO, Mailpit, Kafka UI
	docker compose up -d --wait

down:          ## Stop the local stack (keeps volumes)
	docker compose down

logs:
	docker compose logs -f --tail=100

build:
	dotnet build Plastoids.sln -c Release

test: test-unit test-integration

test-unit:     ## Unit + architecture tests (no Docker needed)
	dotnet test tests/Plastoids.SharedKernel.Tests -c Release --nologo
	dotnet test tests/Plastoids.ArchitectureTests -c Release --nologo

test-integration: ## Testcontainers (needs Docker)
	dotnet test tests/Plastoids.IntegrationTests -c Release --nologo

migrate:       ## Apply all pending EF migrations to the configured MySQL
	dotnet run --project $(API) -- migrate

seed:          ## Seed reference data (tenant zero, chart of accounts) — arrives in Sprint 1/3
	@echo "seed: nothing to seed yet (Sprint 1 adds tenant zero and staff user; Sprint 3 adds the chart of accounts)"

run:           ## Run the API in Development (migrates on startup)
	ASPNETCORE_ENVIRONMENT=Development dotnet run --project $(API)

openapi:       ## Export OpenAPI to artifacts/openapi.json (API must be running on :5080)
	curl -sf http://localhost:5080/openapi/v1.json -o artifacts/openapi.json && echo "wrote artifacts/openapi.json"

format:
	dotnet format Plastoids.sln

clean:
	dotnet clean Plastoids.sln >/dev/null; find . -name bin -o -name obj | xargs rm -rf
