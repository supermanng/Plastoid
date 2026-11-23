.PHONY: up down logs build test test-unit test-integration migrate seed run openapi format clean web web-install web-build web-lint simulate

API := src/Plastoids.Api/Plastoids.Api.csproj

up:            ## Start MySQL, Kafka, Redis, S3Mock, Mailpit, Kafka UI
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
	dotnet test tests/Plastoids.Modules.Identity.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Marketplace.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Money.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Sessions.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Learning.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Sponsorship.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Payouts.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Notifications.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.BackOffice.Tests -c Release --nologo
	dotnet test tests/Plastoids.Modules.Wallet.Tests -c Release --nologo

test-integration: ## Testcontainers (needs Docker)
	dotnet test tests/Plastoids.IntegrationTests -c Release --nologo

migrate:       ## Apply all pending EF migrations to the configured MySQL
	dotnet run --project $(API) -- migrate

seed:          ## Seed tenant zero, the staff user and three learners (runs automatically with Seed:Enabled, i.e. `make run`)
	ASPNETCORE_ENVIRONMENT=Development Seed__Enabled=true dotnet run --project $(API) -- migrate

run:           ## Run the API in Development (migrates on startup)
	ASPNETCORE_ENVIRONMENT=Development dotnet run --project $(API)

openapi:       ## Export OpenAPI to artifacts/openapi.json (API must be running on :5080)
	curl -sf http://localhost:5080/openapi/v1.json -o artifacts/openapi.json && echo "wrote artifacts/openapi.json"

web-install:   ## Install frontend dependencies (frontend/)
	npm ci --prefix frontend --no-audit --no-fund

web:           ## Run the React frontend on :5173 (proxies /api to the API on :5080)
	npm run dev --prefix frontend

web-build:     ## Production build of the frontend to frontend/dist
	npm run build --prefix frontend

web-lint:      ## Lint the frontend
	npm run lint --prefix frontend

simulate:      ## Demo simulation against the running Development API (:5080) and web app (:5173); report in scripts/.simulate/last-run.md
	python3 scripts/simulate.py

format:
	dotnet format Plastoids.sln

clean:
	dotnet clean Plastoids.sln >/dev/null; find . -name bin -o -name obj | xargs rm -rf
