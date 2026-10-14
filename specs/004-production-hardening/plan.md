# Implementation Plan: Production Hardening

**Branch**: `004-production-hardening` | **Date**: 2026-10-14 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-production-hardening/spec.md`. Design source: [docs/hardening-modernization-plan.md](../../docs/hardening-modernization-plan.md).

## Summary

Taskify works as a demo but cannot be trusted at scale. Four stories fix that, in order, without changing anything a team member sees:

- **P1** makes the tests and the pipeline trustworthy: real PostgreSQL through the full HTTP pipeline, one schema writer, image builds and a coverage gate.
- **P2** makes the API consistent and self-describing: Minimal APIs, framework validation, one error format, shared contracts and a typed hub.
- **P3** has the server vouch for the Active User: an API-issued JWT after the user picker.
- **P4** makes production safe with several instances: a Redis backplane, Blazor affinity, internal API ingress, probes, Azure Monitor export and no retries of changes.

The stack is unchanged apart from Redis. The schema is unchanged. Every choice below was checked against current documentation in [research.md](research.md).

## Technical Context

**Language/Version**: C# 14 on .NET 10, the SDK default; `net10.0` set in `Directory.Build.props`.

**Primary Dependencies**:
- ASP.NET Core 10: Minimal APIs, `AddValidation`, ProblemDetails, OpenAPI 3.1, SignalR, JWT bearer.
- Blazor Web App in Interactive Server mode.
- EF Core 10 with Npgsql and EFCore.NamingConventions.
- Aspire 13.1: AppHost and ServiceDefaults, with PostgreSQL and Redis hosting.
- OpenTelemetry, with the Azure Monitor distro in production.
- `Microsoft.Extensions.Http.Resilience` and `Microsoft.Extensions.ServiceDiscovery`.

**Storage**: PostgreSQL 16, on Azure Database for PostgreSQL Flexible Server in production and an Aspire container locally. **No schema change** (see [data-model.md](data-model.md)). Redis is added for SignalR pub/sub only, with no persisted data: Azure Managed Redis B0 in Azure and an Aspire container locally.

**Testing**:
- xUnit 2 with `WebApplicationFactory<Program>` and Testcontainers (`postgres:16`, plus Redis for the multi-instance test).
- bUnit 2 for components.
- `FakeTimeProvider` and `MetricCollector<T>`.
- coverlet with ReportGenerator for the 80% gate.
- BenchmarkDotNet against PostgreSQL.

**Target Platform**: Linux containers on Azure Container Apps, consumption plan. Local development through the Aspire AppHost.

**Project Type**: Web service (`Taskify.Api`) plus server-rendered web app (`Taskify.Web`), orchestrated by Aspire.

**Performance Goals**: Constitution budgets: API reads p95 ≤ 200 ms, writes p95 ≤ 500 ms. SC-004: the task list for 500 Tasks × 20 Comments within the read budget. SC-006: cross-instance live updates within 2 s.

**Constraints**:
- No visible change to the board (FR-041).
- Existing validation rules are kept (FR-042).
- No secrets in the repository (FR-030).
- Cyclomatic complexity ≤ 10 per method.
- Every package version lives in `Directory.Packages.props` only.

**Scale/Scope**: 5 Users, 3 Projects, up to about 500 Tasks per Project. The API runs 1–10 replicas. The web app runs 1–10 in production and 0–10 in dev. Six projects in `src/` and three in `tests/`; no project is added or removed.

## Constitution Check

*GATE: must pass before Phase 0 research. Re-checked after Phase 1 design (see the end of this section).*

| Gate | Requirement | Status before this feature | How this plan meets it | Result |
|---|---|---|---|---|
| I. Code quality | Linting and static analysis configured; complexity enforced in CI; naming documented | Analyzers and warnings-as-errors are on; `dotnet format` runs in CI; naming rules are in `.editorconfig`. Complexity ≤ 10 is **not** enforced: SonarAnalyzer S1541 is off by default. | P1 enables S1541 in `.editorconfig`, which warnings-as-errors makes a build failure (research R5). | PASS, with a P1 action |
| II. Test-driven | Framework configured; TDD active; coverage enforced in CI; contract tests for new API surface; one integration test per story | xUnit and bUnit are configured. Coverage is collected but **not enforced**. No integration test touches real PostgreSQL. | P1 builds the real-database harness first (research R1) and two coverage gates in CI (R4): 80% overall, and 95% on critical paths (the API's `Identity`, `Services` and `Endpoints` code, covering auth, data persistence and Task changes). Every later story starts with failing tests. Contract tests: the OpenAPI test (R8), and a hub contract test that sends through `ITaskifyClient`. Each story has its own integration tests (see "Design by story"). | PASS, with P1 actions |
| III. UX consistency | Design tokens imported; accessibility tooling configured; design spec linked | Tokens exist (`wwwroot/css/tokens.css`). No accessibility tooling in CI. | This feature changes no UI (FR-041). The only markup changes are `@key` and `[PersistentState]`, which render nothing new. Existing bUnit tests and SC-012 guard against regressions. The missing accessibility tooling is a pre-existing gap, logged below. | PASS, with a logged gap |
| IV. Performance | Benchmark suite exists; CI has a performance regression step with budgets | BenchmarkDotNet runs weekly and does not block PRs. It benchmarks the InMemory provider, which measures nothing real. | P1 points the benchmarks at PostgreSQL and adds the 500 × 20 task-list benchmark (SC-004). A per-PR regression gate that blocks a >10% slowdown is a pre-existing gap, logged below. | PASS, with a logged gap |

**Post-design re-check (after Phase 1)**: the contracts, data model and test design above introduce no new violations. Design rules hold:
- Each mechanism has one place: one JSON configuration, one outcome mapping, one hub interface, one schema writer.
- C# 14 features are used only for an extension property and one setter.
- No new project or repository layer is added.

The two logged gaps are unchanged.

## Project Structure

### Documentation (this feature)

```text
specs/004-production-hardening/
├── spec.md              # Feature specification
├── plan.md              # This file
├── research.md          # Phase 0: decisions and sources
├── data-model.md        # Phase 1: no schema change; contract types
├── quickstart.md        # Phase 1: validation guide per story
├── contracts/
│   ├── rest-api.md      # 2.0.0, supersedes specs/001-create-taskify/contracts/rest-api.md
│   └── signalr-hub.md   # 2.0.0, supersedes specs/001-create-taskify/contracts/signalr-hub.md
├── checklists/requirements.md
└── tasks.md             # Phase 2 (/speckit.tasks, not created here)
```

### Source code (repository root)

Legend: `+` new, `~` changed, `-` removed.

```text
src/
├── Taskify.Api/
│   ├── Program.cs                       ~ P2 endpoint groups, validation, ProblemDetails; P3 auth; P4 Redis; MigrateAsync removed (P1)
│   ├── Controllers/                     - P2 (all five controllers)
│   ├── Endpoints/                       + P2: ProjectEndpoints, TaskEndpoints, CommentEndpoints, UserEndpoints, NotificationEndpoints
│   ├── Results/                         + P2: Result.cs (Result<T>, Error, ErrorKind), ResultHttpExtensions.cs (ToHttpResult)
│   ├── ErrorHandling/                   + P2: UnexpectedExceptionHandler.cs (IExceptionHandler)
│   ├── Identity/                        + P3: AuthEndpoints, TokenIssuer, AuthOptions, ClaimsPrincipalExtensions (C# 14 extension property)
│   ├── Observability/                   + P4: TaskifyMetrics.cs (Meter "Taskify.Api"), Log.cs ([LoggerMessage])
│   ├── Hubs/TaskifyHub.cs               ~ P2 Hub<ITaskifyClient>; P3 [Authorize]
│   ├── Services/TaskService.cs          ~ P2 Result<T>, projection, CancellationToken, TimeProvider, typed hub; P3 caller from claims; P4 metric, safe broadcast
│   ├── Services/CommentService.cs       ~ same as TaskService
│   └── Data/                            (unchanged: entities, DbContext, migrations)
├── Taskify.Shared/
│   ├── Taskify.Shared.csproj            ~ P2 reference Microsoft.Extensions.Validation
│   ├── Dtos/                            ~ P3 TokenResponse
│   ├── Requests/                        + P2 request records (see data-model.md); P3 TokenRequest
│   ├── Realtime/                        + P2 ITaskifyClient and five *Message records
│   ├── TaskifyJson.cs                   + P2 the single JSON configuration
│   └── ValidationRegistration.cs        + P2 AddTaskifyContractValidation(), which calls AddValidation in this assembly
├── Taskify.Web/
│   ├── Program.cs                       ~ P3 named "auth" client; P4 resolver for the hub
│   ├── Services/ApiClient.cs            ~ P2 shared records, TaskifyJson, CancellationToken, split update; P3 bearer header
│   ├── Services/BoardHubClient.cs       ~ P2 shared messages via nameof; P3 AccessTokenProvider; P4 WebSockets with SkipNegotiation and resolved URL
│   ├── Services/IdentityService.cs      ~ P2 field-backed CurrentUser; P3 token, expiry, silent renewal
│   └── Components/                      ~ P2 @key in KanbanBoard, [PersistentState] in Home and ProjectList, TaskDetail uses the split update
├── Taskify.ServiceDefaults/Extensions.cs ~ P4 UseAzureMonitor, DisableForUnsafeHttpMethods, AddMeter("Taskify.Api")
├── Taskify.AppHost/Program.cs           ~ P3 auth-signing-key parameter; P4 AddRedis
└── Taskify.Migrator/Program.cs          ~ P4 [LoggerMessage]

src/Taskify.Api/Dockerfile, src/Taskify.Web/Dockerfile   ~ P1 copy Directory.Packages.props

tests/
├── Taskify.Api.Tests/
│   ├── Infrastructure/                  + P1 SharedPostgres (one container per run), TaskifyApiFixture (one database per class), HubTestClient; P3 token helper; P4 SharedRedis
│   ├── Endpoints/                       + P1 HTTP tests per resource (replacing Controllers/); grown in P2 and P3
│   ├── Contract/                        + P2 OpenApiContractTests, HubContractTests ([Trait("Category","Contract")])
│   ├── Data/                            + P1 SchemaOwnershipTests (the API never migrates); P2 TaskListQueryTests (SQL shape)
│   ├── Identity/                        + P3 TokenEndpointTests, AuthorizationTests, HubAuthorizationTests
│   ├── Realtime/                        + P1 BroadcastTests (real subscriber); P4 MultiInstanceTests (two hosts plus Redis)
│   ├── Observability/                   + P4 TaskMoveMetricTests
│   └── Controllers/, Helpers/TestHelpers.cs   - P1 (InMemory and fake hub context)
├── Taskify.Web.Tests/                   ~ P2 or P3 updated for the split update and tokens; + Services/ResilienceTests (P4), IdentityServiceRenewalTests (P3)
└── Taskify.Benchmarks/                  ~ P1 PostgreSQL container; + TaskListBenchmark (500 × 20)

infra/resources/container-apps.bicep     ~ P4 internal API ingress, web affinity, single revision mode, probes, Redis and signing-key secrets
infra/resources/redis.bicep              + P4 Azure Managed Redis B0
infra/main.bicep, main.parameters.*.json ~ P3 signing-key parameter; P4 Redis module, web minReplicas 1 in prod
infra/hooks/postdeploy.sh                ~ P4 smoke tests through the web app
.github/workflows/ci.yml                 ~ P1 coverage gate, image builds
.github/workflows/azure-dev.yml          ~ P3 pass the signing key; P4 smoke tests through the web app
.editorconfig                            ~ P1 S1541; P2 CA2016
dotnet-tools.json                        ~ P1 ReportGenerator
Directory.Packages.props                 ~ see "Package changes"
docs/adr/                                + ADRs 0001–0004
CONTEXT.md, README.md                    ~ P3 Active User; README per story
```

**Structure decision**: keep the existing solution layout. New code goes in folders named after what it does inside the existing projects. `Endpoints/` replaces `Controllers/`; `Requests/` and `Realtime/` hold the contracts in `Taskify.Shared`. No project is added, so the solution graph and Dockerfiles stay the same shape.

## Design by story

Each story starts with failing tests (TDD) and ends with green CI, updated README claims and the story's quickstart section passing.

### P1 – Trustworthy verification (FR-001–FR-010)

1. **Harness (research R1)**:
   - `SharedPostgres` starts `postgres:16` once per run.
   - `TaskifyApiFixture` creates a database per test class, runs the real migrations, and hosts the API with `UseSetting("ConnectionStrings:taskifydb", …)`.
   - Make the API visible to tests with `public partial class Program;`.
2. **Rewrite the existing API tests as HTTP tests** against today's controllers, keeping behaviour identical. This proves the harness before P2 changes the API.
3. **Validation proof (FR-004)**: an invalid Status through the full pipeline returns 400 Problem Details, and the database is unchanged.
4. **Live-update proof (FR-005)**: a `HubTestClient` (long polling over `TestServer`) joins a board and receives `TaskMoved`.
5. **Single schema writer (FR-006, research R2)**: remove `MigrateAsync` from the API. `SchemaOwnershipTests` starts the API against an empty database and asserts that `__EFMigrationsHistory` does not exist afterwards.
6. **Images (FR-007, research R3)**: copy `Directory.Packages.props` in both Dockerfiles, and add a `docker build` job for both images to `ci.yml`.
7. **Coverage gates (FR-008, constitution Principle II, research R4)**: `--settings coverage.runsettings` and a ReportGenerator merge. Fail below 80% overall, and below 95% on the API's `Identity`, `Services` and `Endpoints` code. Print both figures.
8. **Complexity (gate I, research R5)**: enable S1541.
9. **Benchmarks (gate IV)**: point them at the PostgreSQL container, and remove the InMemory provider from every project (FR-002).
10. **README (FR-010)**: describe the real-database tests.

### P2 – Consistent, self-describing API (FR-011–FR-020)

1. **Contracts first**:
   - Add `TaskifyJson`, the request records, `ITaskifyClient` and the message records to `Taskify.Shared`.
   - Add `AddTaskifyContractValidation()` (research R6, R9, R10).
2. **Outcome model (research R7)**: `Result<T>`, `Error`, `ErrorKind` and `ToHttpResult()`, plus `AddProblemDetails`, `UseExceptionHandler` and `UnexpectedExceptionHandler`.
3. **Endpoints**: one class per resource, returning `TypedResults` through `ToHttpResult()`, with `.ProducesProblem(…)` and `.ProducesValidationProblem()` (research R8). Delete `Controllers/` and `UpdateTaskRequestConverter`.
4. **Task update split (FR-015)**: `PUT /api/tasks/{id}` and `PATCH /api/tasks/{id}/assignee`. `ApiClient` and `TaskDetail` follow.
5. **Queries (FR-014, research R11)**: a shared projection with an SQL count, plus `AsNoTracking`. `TaskListQueryTests` checks the SQL.
6. **Cancellation and time (FR-016, research R12)**: a `CancellationToken` on every handler, service method and `ApiClient` method; `CancellationToken.None` for post-save broadcasts; `TimeProvider` injected; CA2016 raised to a warning.
7. **Typed hub (FR-018)**: `Hub<ITaskifyClient>` and `IHubContext<TaskifyHub, ITaskifyClient>`. `BoardHubClient` registers handlers with `nameof`.
8. **Blazor (research R13)**: `@key` and `[PersistentState]`. Use `field` in `IdentityService.CurrentUser` to raise `OnChange`.
9. **Contract tests**: OpenAPI completeness (SC-003) and the hub message round trip.

### P3 – Server-vouched identity (FR-021–FR-031)

1. **Token endpoint**: `TokenIssuer` creates the token with `JsonWebTokenHandler`, using `TimeProvider` for `exp`; `AuthEndpoints` maps `POST /api/auth/token`.
2. **Authentication and authorization**:
   - JWT bearer with `MapInboundClaims = false`.
   - `RequireAuthorization()` on every group except the token endpoint and `GET /api/users` (FR-025).
   - `[Authorize]` on `TaskifyHub`.
   - `ClaimsPrincipal.UserId` as a C# 14 extension property.
3. **Caller from claims (FR-023, FR-026, FR-027)**: remove `authorId` and `requestingUserId`. The services take the caller id from the endpoint, which reads `user.UserId`.
4. **Web app (FR-029)**:
   - `IdentityService` gets and renews the token through the named `auth` client, guarded by a `SemaphoreSlim`.
   - `ApiClient` sets the bearer header in its single send helper.
   - `BoardHubClient` sets `AccessTokenProvider`.
5. **Secret (FR-030)**:
   - Locally: the Aspire parameter `auth-signing-key` (secret), passed as `Auth__SigningKey`.
   - In Azure: a `@secure()` Bicep parameter, fed from a GitHub secret by `azure-dev.yml` into a Container Apps secret.
6. **Documentation (FR-031)**: ADR 0002, and the Active User definition in `CONTEXT.md`.

### P4 – Safe to run at scale (FR-032–FR-040)

1. **Backplane (research R15, R16)**:
   - `AddRedis("redis")` in the AppHost.
   - `AddStackExchangeRedis(connectionString, ChannelPrefix "taskify")` in the API.
   - `infra/resources/redis.bicep` adds Azure Managed Redis B0, with high availability in prod only.
   - Broadcast failures are caught, logged and counted.
2. **Hub connection (research R15)**: `BoardHubClient` resolves the API endpoint with `ServiceEndpointResolver`, and uses WebSockets with `SkipNegotiation`.
3. **Multi-instance proof (SC-006, research R23)**: `MultiInstanceTests` runs two API hosts against shared PostgreSQL and Redis.
4. **Container Apps (research R17–R19)**:
   - API: `external: false`.
   - Web app: sticky sessions and `activeRevisionsMode: 'Single'`.
   - Both apps: startup, liveness and readiness probes.
   - Web `minReplicas: 1` in `main.parameters.prod.json`.
5. **Telemetry (research R20, R22)**:
   - `UseAzureMonitor()` when the connection string is set.
   - `TaskifyMetrics` with the `taskify.tasks.moved` counter, plus `AddMeter`.
   - `[LoggerMessage]` logging.
6. **Resilience (research R21)**: `DisableForUnsafeHttpMethods()`. `ResilienceTests` checks that a failing `POST` is sent once and a failing `GET` more than once.
7. **Smoke tests (research R18)**: `postdeploy.sh` checks the web app, and checks that the API is not reachable from outside.
8. **README (FR-040)**:
   - Monitoring now reaches Application Insights.
   - The health endpoints are `/health` and `/alive`, used as probes.
   - Notifications are no longer listed as a feature.
9. **ADRs**: 0003 (Redis backplane) and 0004 (internal API ingress).

## Package changes (`Directory.Packages.props`)

Use the latest stable 10.0.x or 13.1.x patch at implementation time.

| Package | Project | Story | Change |
|---|---|---|---|
| `Microsoft.Extensions.Validation` | Taskify.Shared | P2 | add |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | Taskify.Api | P3 | add |
| `Microsoft.AspNetCore.SignalR.StackExchangeRedis` | Taskify.Api | P4 | add |
| `Aspire.Hosting.Redis` | Taskify.AppHost | P4 | add (13.1.x, matching the other hosting packages) |
| `Azure.Monitor.OpenTelemetry.AspNetCore` | Taskify.ServiceDefaults | P4 | add |
| `Testcontainers.Redis` | Taskify.Api.Tests | P4 | add (4.15.0, matching Testcontainers.PostgreSql) |
| `Microsoft.Extensions.TimeProvider.Testing` | test projects | P2 | add |
| `Microsoft.Extensions.Diagnostics.Testing` | Taskify.Api.Tests | P4 | add |
| `Microsoft.EntityFrameworkCore.InMemory` | Api.Tests, Benchmarks | P1 | **remove** |
| `dotnet-reportgenerator-globaltool` | `dotnet-tools.json` | P1 | add a local tool |

## Architecture decision records

These follow `.agents/skills/domain-modeling/ADR-FORMAT.md`. Each is written in the story that makes the decision.

| ADR | Decision | Story |
|---|---|---|
| `docs/adr/0001-minimal-apis.md` | Minimal APIs with `AddValidation` and `TypedResults` replace controllers (D1) | P2 |
| `docs/adr/0002-api-issued-identity.md` | The API issues a short-lived JWT after the user picker; the caller comes from claims (D2) | P3 |
| `docs/adr/0003-redis-signalr-backplane.md` | A Redis backplane on Azure Managed Redis B0, with WebSockets only from the web app so the API needs no affinity (D3) | P4 |
| `docs/adr/0004-internal-api-ingress.md` | The API is internal to the Container Apps environment; smoke tests go through the web app (D4) | P4 |

`CONTEXT.md`: Active User becomes "the User a browser session acts as, chosen at the landing screen and vouched for by the server through a short-lived identity" (FR-031, P3).

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Validation metadata silently missing for types in `Taskify.Shared` (research R6) | `AddTaskifyContractValidation()`, plus a 400 test for every operation that takes input. |
| `UseSetting` not reaching the Aspire `AddNpgsqlDbContext` registration in `WebApplicationFactory` | The first P1 task proves the fixture against a real database. Fallback: environment variables set before the factory starts. |
| Azure Managed Redis Bicep details (SKU name, clustering policy enum, access keys off by default) | Confirm with `az deployment group what-if` in dev before prod. Enable access-key authentication explicitly. |
| Concurrent token renewals within one circuit | A `SemaphoreSlim` in `IdentityService`, plus a renewal test with `FakeTimeProvider`. |
| A Blazor circuit is lost when its replica is replaced | Accepted in the spec: the team member may need to pick themselves again. |
| Migration speed or flakiness in CI with containers | One container per run; a database per class is fast to create; the GitHub Ubuntu runners have Docker. |

## Complexity Tracking

These are not violations introduced by this feature. They are pre-existing gaps against the constitution's gates, logged here as Technical Quality Gates require, each with a confirmed remediation date.

| Gap | Why it stays open in this feature | Remediation |
|---|---|---|
| No accessibility tooling (axe-core) in CI (gate III) | This feature changes no UI (FR-041). Adding tooling is UI-test infrastructure of its own. | A separate feature before the next UI change, by 2027-01-31. |
| Benchmarks run weekly and do not block PRs on a >10% regression (gate IV) | A stable per-PR comparison needs a baseline store and noise control, which is out of this spec's scope. SC-004 is still measured. | A separate feature: a benchmark comparison job on PRs against `main`, by 2027-01-31. |
