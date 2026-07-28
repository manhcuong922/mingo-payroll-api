# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

MPS (Mingo Payroll System) — a Vietnamese HR/payroll REST API built on .NET 8 (ASP.NET Core) using Clean Architecture. Code comments and many docs are in Vietnamese; keep that convention when editing existing files. Major modules: Auth/RBAC, Employee, Contract (templates, parties, appendices, terminations), Compliance (labor-law constants, PIT, social insurance, minimum wage), Compliance Auto-Update (LLM-assisted legal crawling), OCR (citizen ID), and Audit Logging.

## Common Commands

Run from the repo root (the `MPS.sln` directory). The shell here is PowerShell.

```bash
dotnet restore MPS.sln
dotnet build MPS.sln -c Release
dotnet run --project src/MPS.API            # serves http://localhost:5000 (Swagger UI at / in Development)

# Tests (xUnit + FluentAssertions + Moq)
dotnet test MPS.sln                          # all tests
dotnet test tests/MPS.Application.Tests      # one test project
dotnet test --filter "FullyQualifiedName~PositionCreateCommandHandler"   # single test/class

# EF Core migrations (run from repo root; --project = where migrations live, --startup-project = API)
dotnet ef migrations add <Name> --project src/MPS.Infrastructure --startup-project src/MPS.API
dotnet ef database update --project src/MPS.Infrastructure --startup-project src/MPS.API
```

CI (`.github/workflows/ci.yml`) runs `restore` → `build -c Release` → `test` on every PR into `dev`. The default branch for PRs is `main`; feature work happens on `dev`.

Note: migrations are applied automatically at startup (`db.Database.MigrateAsync()` in `Program.cs`), which also seeds a default admin via `DatabaseSeeder`. You rarely need to run `database update` manually for local dev.

## Architecture

Five projects under `src/`, wired by Clean Architecture dependency rules (dependencies point inward; Domain depends on nothing):

- **MPS.Domain** — Entities, Enums, domain Exceptions, Constants (`Permissions`, `SystemRoles`, `LaborLawConstants`), and **repository interfaces** (`Domain/Interfaces/I*Repository.cs`). All entities derive from `BaseEntity` (`Id`, `CreatedAt`, `UpdatedAt`). Note: `Id` is `Guid` on `BaseEntity` but several entities/repositories use `int` keys — check the specific repository interface.
- **MPS.Shared** — Cross-cutting primitives shared by all layers.
- **MPS.Application** — Use cases via **MediatR (CQRS)**. Organized by feature under `Features/<Feature>/{Commands,Queries,Validators}`. Each command/query is a `record` implementing `IRequest<T>` with a sibling `...Handler`. Validation uses **FluentValidation**; mapping uses **AutoMapper** (`Mappings/`). Depends only on Domain + Shared.
- **MPS.Infrastructure** — Implementations: EF Core `AppDbContext`, repositories (`Repositories/`), entity configs (`Persistence/Configurations/`), migrations, security (BCrypt, JWT), MinIO storage, SMTP email, OCR, background jobs, and the Compliance Auto-Update pipeline. Everything is registered in `DependencyInjection/InfrastructureServiceRegistration.cs` (`AddInfrastructure`).
- **MPS.API** — Thin controllers that inject `IMediator` and `Send` commands/queries. Also holds request/response DTOs (`Requests/`, `Responses/`), middleware, and the permission authorization system. `Program.cs` is the composition root.

### Request flow

Controller → `IMediator.Send` → MediatR pipeline behaviors → Handler → Repository → EF Core / external service.

MediatR pipeline order (registered in `Program.cs`): `ArchivedContractGuardBehavior` (fail-fast 409 on archived contracts) → `ValidationBehavior` (runs FluentValidation, throws `ValidationException`) → Handler.

### Conventions to follow when adding a feature

1. Add the command/query `record` + handler under `Application/Features/<Feature>/{Commands,Queries}`; add a FluentValidation validator under `Validators/`.
2. Define the repository interface in `Domain/Interfaces/`, implement it in `Infrastructure/Repositories/`, and **register it** in `InfrastructureServiceRegistration.cs`.
3. Add the controller action; guard it with `[HasPermission(Permissions.X)]` (or `[HasAnyPermission]` / `[HasAllPermissions]`) and add the permission constant to `Domain/Constants/Permissions.cs`.
4. Throw domain/application exceptions (e.g. `NotFoundException`, `ConflictException`, `DomainValidationException`) rather than returning ad-hoc error payloads — `ExceptionHandlingMiddleware` maps each exception type to the correct HTTP status and JSON shape.

### Authorization

Custom permission-based RBAC, not role checks. `[HasPermission("...")]` produces an authorization policy resolved by `PermissionPolicyProvider` and checked by `PermissionAuthorizationHandler` (permissions resolved per-user via `IPermissionResolver`). Denied access is audited by `AuditAccessDeniedHandler` before returning 403. JWT bearer auth is configured in `Program.cs`; `MapInboundClaims = false`.

### Cross-cutting infrastructure

- **Auditing**: `AuditSaveChangesInterceptor` writes audit entries on `SaveChanges`; severity is classified by `IAuditSeverityClassifier`; retention/archival handled by `AuditLogRetentionJob` (config under `AuditLog`).
- **Background jobs** (`IHostedService` in `Infrastructure/BackgroundJobs/`): `ContractAutoConvertJob`, `ContractExpiryJob`, `ContractArchiveJob`, `ComplianceActivationJob`, `ComplianceWatcherJob`, `RefreshTokenCleanupService`, `AuditLogRetentionJob`. Several are registered as singletons *and* hosted services so controllers can trigger a manual `RunOnceAsync`.
- **Storage**: MinIO (S3-compatible) via `IMinioClient`; presigned URLs; health check exposed at `/health/minio`.
- **Rate limiting**: named policies in `Program.cs` (`auth-login`, `auth-refresh`, `contract-upload`, `ocr-citizen-id`).

### Compliance Auto-Update (LLM)

`Infrastructure/ComplianceAutoUpdate/` crawls Vietnamese legal sources (`vbpl.vn`, `congbao.chinhphu.vn` via `ILegalSourceAdapter`), extracts changes, and produces reviewable proposals. The LLM extractor/transcriber/discoverer is **Anthropic Claude** when `provider=anthropic` and the API key env var (default `OPERIX_LLM_API_KEY`) is set; otherwise the code falls back to offline regex-based implementations (no network). Models and behavior are configured under `ComplianceAutoUpdate` in `appsettings.json` (extractor `claude-opus-4-8`, OCR `claude-haiku-4-5-20251001`, watcher `claude-sonnet-4-6`). When touching LLM code, consult the `claude-api` skill for current model IDs/pricing. `BaseUrl` empty = call Anthropic directly; dev can route through a CLIProxyAPI (see `deploy/llmproxy`).

## Configuration & Secrets

`appsettings.json` holds non-secret defaults; **secret fields are intentionally blank**. Local dev overrides go in `appsettings.Development.json` (gitignored) or user-secrets (`UserSecretsId` in `MPS.API.csproj`). Production injects secrets via environment variables using the `Section__Key` convention (e.g. `ConnectionStrings__DefaultConnection`, `Jwt__SecretKey`, `Smtp__Password`, `Minio__SecretKey`, `OPERIX_LLM_API_KEY`). The app fails fast at startup if `Jwt:SecretKey` is missing/<32 bytes or required `Minio`/`Compliance`/`AuditLog` options are invalid (`.ValidateOnStart()`).

Default local DB is SQL Server (`ConnectionStrings:DefaultConnection`). Tests use EF Core InMemory.

## Deployment

Multi-stage `Dockerfile` (SDK build → `aspnet:8.0` runtime, runs as non-root user `app`, listens on `:8080`). Production stack and deploy instructions are under `deploy/` (`docker-compose.prod.yml`, `Caddyfile`, `deploy.sh`, `HUONG-DAN-TRIEN-KHAI.md`). `.github/workflows/deploy.yml` handles CD.

## Further docs

Module plans and API contracts live in `docs/` (`docs/plans/*-implementation-plan.md`, `docs/compliance-api-contract.md`, `docs/postman/`, plus per-module READMEs under `docs/auth`, `docs/contracts`, `docs/storage`). `CONTRACT_DB_PLAN.md` at the root details the contract schema.

## Project rules

`.claude/rules/common/` contains enforced standards: immutable/KISS/DRY coding style, files <800 lines & functions <50 lines, 80% min test coverage with TDD (RED-GREEN-REFACTOR), mandatory security checks before commits, and conventional-commit messages (`feat|fix|refactor|docs|test|chore|perf|ci`). Review these when in doubt.
