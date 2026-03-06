# Implementation Plan: Create Taskify

**Branch**: `001-create-taskify` | **Date**: March 5, 2026 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-create-taskify/spec.md`

## Summary

Build Taskify: a Kanban-style team productivity platform for five predefined users across three sample projects. The frontend is a Blazor Server single-page application with drag-and-drop task boards and real-time multi-user updates delivered via SignalR. A .NET Aspire AppHost orchestrates the solution (Blazor Server frontend, ASP.NET Core REST API, PostgreSQL). The API exposes Users, Projects, Tasks, Comments, and Notifications resources. No authentication layer — users identify themselves by clicking their name on a landing screen.

## Technical Context

**Language/Version**: C# 14 / .NET 10
**Primary Dependencies**:
- .NET Aspire 10 (orchestration, service discovery, OpenTelemetry)
- Blazor Server (interactive UI, component model)
- ASP.NET Core Web API (REST endpoints)
- SignalR (real-time board updates via `TaskifyHub`)
- Entity Framework Core 10 + Npgsql provider (ORM)
- SortableJS via JS interop (drag-and-drop on Blazor Server)
- xUnit 2 (unit & integration tests)
- bUnit 2.x (Blazor component unit tests)
- Testcontainers.PostgreSQL (integration tests with real DB)
- Coverlet (code coverage reporting)
- CSharpier (code formatting) + SonarAnalyzer.CSharp (static analysis / cyclomatic complexity)

**Storage**: PostgreSQL 16 (hosted via `Aspire.Hosting.PostgreSQL`)
**Testing**: xUnit + bUnit + Testcontainers; coverage enforced via Coverlet in CI
**Target Platform**: Web browser (Chrome/Firefox/Edge); local dev via .NET Aspire dashboard
**Project Type**: Multi-project web application (.NET solution)
**Performance Goals**: Initial screen render ≤ 2 000 ms; API reads p95 ≤ 200 ms; API writes p95 ≤ 500 ms; drag-and-drop at 60 fps; board push latency ≤ 3 000 ms
**Constraints**: No authentication middleware; no cross-refresh session persistence; cyclomatic complexity ≤ 10 per function
**Scale/Scope**: 5 predefined users, 3 projects, ~30 seed tasks, Phase 1 MVP

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Assessment | Status |
|------|-----------|------------|--------|
| Code Quality | Every function ≤ 10 cyclomatic complexity; CSharpier + SonarAnalyzer enforced in CI | All service-layer methods follow single-responsibility; SortableJS interop split across init/dispose helpers; no method expected to exceed complexity 10 | ✅ PASS |
| Testing | ≥ 80% overall coverage; ≥ 95% critical paths (service layer + hub); no test relies on mocks for DB | Testcontainers.PostgreSQL used for integration tests (real DB); bUnit 2.x for all Blazor components; Coverlet gates in CI | ✅ PASS |
| UX Consistency | WCAG 2.1 AA; ≤ 2 000 ms initial render; 60 fps drag-and-drop; axe-core in CI | Design tokens applied throughout; axe-core integrated; SortableJS at 60 fps; Aspire dashboard for perf tracing | ✅ PASS |
| Performance | API reads p95 ≤ 200 ms; writes p95 ≤ 500 ms; SignalR push ≤ 3 000 ms | BenchmarkDotNet for API baselines; k6 load tests included in CI; EF Core query logging for N+1 prevention | ✅ PASS |

## Project Structure

### Documentation (this feature)

```text
specs/001-create-taskify/
├── plan.md              # This file
├── research.md          # Phase 0 decisions (R-001 to R-014)
├── data-model.md        # Entities, relationships, seed data
├── quickstart.md        # Dev onboarding + design tokens
├── contracts/
│   ├── rest-api.md      # 14 REST endpoints (request/response shapes)
│   └── signalr-hub.md  # TaskifyHub (2 C→S methods, 6 S→C events)
└── tasks.md             # Phase 2 output (/speckit.tasks — not created here)
```

### Source Code (repository root)

```text
Taskify.sln
│
├── src/
│   ├── Taskify.AppHost/               # .NET Aspire orchestrator
│   │   ├── Program.cs                 # Resource wiring: postgres, api, web
│   │   └── Taskify.AppHost.csproj
│   │
│   ├── Taskify.ServiceDefaults/       # Shared Aspire defaults (OTel, health, discovery)
│   │   ├── Extensions.cs
│   │   └── Taskify.ServiceDefaults.csproj
│   │
│   ├── Taskify.Shared/                # DTOs + contract types shared by Api and Web
│   │   ├── Dtos/
│   │   │   ├── UserDto.cs
│   │   │   ├── ProjectDto.cs
│   │   │   ├── TaskItemDto.cs
│   │   │   ├── CommentDto.cs
│   │   │   └── NotificationDto.cs
│   │   └── Taskify.Shared.csproj
│   │
│   ├── Taskify.Api/                   # ASP.NET Core Web API (REST + SignalR)
│   │   ├── Controllers/
│   │   │   ├── UsersController.cs
│   │   │   ├── ProjectsController.cs
│   │   │   ├── TasksController.cs
│   │   │   ├── CommentsController.cs
│   │   │   └── NotificationsController.cs
│   │   ├── Hubs/
│   │   │   └── TaskifyHub.cs          # SignalR hub
│   │   ├── Services/
│   │   │   ├── TaskService.cs         # LWW updates via ExecuteUpdateAsync
│   │   │   └── CommentService.cs      # Ownership-gated delete (403)
│   │   ├── Data/
│   │   │   ├── TaskifyDbContext.cs
│   │   │   └── Migrations/
│   │   ├── Program.cs
│   │   └── Taskify.Api.csproj
│   │
│   └── Taskify.Web/                   # Blazor Server frontend
│       ├── Services/
│       │   ├── ApiClient.cs           # Typed HttpClient (Aspire service discovery)
│       │   ├── BoardHubClient.cs      # SignalR HubConnection (+ handler bridge)
│       │   └── IdentityService.cs     # Scoped; stores current user (no auth)
│       ├── Components/
│       │   ├── App.razor
│       │   ├── Routes.razor
│       │   ├── Layout/
│       │   │   └── MainLayout.razor
│       │   └── Pages/
│       │       ├── UserSelection.razor
│       │       ├── ProjectList.razor
│       │       ├── KanbanBoard.razor
│       │       ├── TaskDetail.razor
│       │       └── Shared/
│       │           ├── TaskCard.razor
│       │           └── CommentItem.razor
│       ├── wwwroot/
│       │   ├── css/app.css            # Design tokens + Kanban grid
│       │   └── js/
│       │       └── sortable-interop.js  # ES module SortableJS shim
│       ├── Program.cs
│       └── Taskify.Web.csproj
│
└── tests/
    ├── Taskify.Api.Tests/             # xUnit + Testcontainers.PostgreSQL
    │   ├── Controllers/               # Integration tests per controller
    │   ├── Hubs/                      # SignalR hub integration tests
    │   └── Taskify.Api.Tests.csproj
    ├── Taskify.Web.Tests/             # bUnit 2.x component tests
    │   ├── Components/                # One test class per Blazor component
    │   └── Taskify.Web.Tests.csproj
    └── Taskify.Benchmarks/            # BenchmarkDotNet
        ├── ApiBenchmarks.cs
        └── Taskify.Benchmarks.csproj
```

**Structure Decision**: Three-tier multi-project .NET 10 solution. Shared DTOs live in `Taskify.Shared` to avoid circular references between `Api` and `Web`. `ServiceDefaults` is the Aspire-standard shared library for OpenTelemetry, health checks, and service discovery wiring. Test projects mirror source project names with `.Tests`/`.Benchmarks` suffix.

## Core Implementation

Steps are sequenced to respect compilation dependencies: `Shared` → `Api` (domain + DB) → `AppHost` wiring → `Web` (client) → UI components → tests → quality gates. Each step cites the artifact(s) that contain exact implementation guidance.

### Step 1 — Solution scaffold & NuGet references

**What**: Create .sln, five source projects, three test projects. Add NuGet packages.
**References**:
- [research.md R-012 §1](research.md) — `dotnet new` commands for Aspire solution layout
- [research.md R-010 §1](research.md) — Typed HttpClient package requirements
- [research.md R-011 §2](research.md) — SignalR client package for Blazor Server

**Key actions**:
1. `dotnet new aspire-starter -n Taskify` (creates AppHost + ServiceDefaults)
2. Add `Taskify.Shared`, `Taskify.Api`, `Taskify.Web` as `classlib` / `web` projects
3. Add test projects (`xunit`, `bunit`, `benchmarks`)
4. Wire project references (Shared ← Api, Shared ← Web, ServiceDefaults ← Api, ServiceDefaults ← Web)

---

### Step 2 — Domain entities + DbContext

**What**: Define EF Core entities, configure `TaskifyDbContext`, add Npgsql + snake_case.
**References**:
- [data-model.md → Entities section](data-model.md) — field names, types, FK relationships
- [research.md R-012 §3](research.md) — `AddNpgsqlDbContext`, `UseSnakeCaseNamingConvention`
- [research.md R-012 §7](research.md) — Npgsql enum mapping (if TaskStatus is a PG enum)

**Key actions**:
1. Create entity classes in `Taskify.Api/Data/` from data-model.md entity table
2. Configure `TaskifyDbContext` with `UseLowerCaseNamingConvention()` / snake_case
3. TDD gate: write entity unit tests (required field validation, FK integrity) **before** running migrations

---

### Step 3 — Migrations + seed data

**What**: Generate EF Core migrations; seed five users, three projects, seed tasks.
**References**:
- [data-model.md → Seed Data section](data-model.md) — exact seed rows
- [research.md R-012 §6](research.md) — `MigrateAsync()` in `Program.cs`

**⚠ Critical rule**: NEVER call `EnsureCreatedAsync()` — it bypasses migrations. Use `MigrateAsync()` only.

**Key actions**:
1. `dotnet ef migrations add InitialCreate -p Taskify.Api`
2. Seed in `TaskifyDbContext.OnModelCreating` using `HasData`
3. Call `await db.Database.MigrateAsync()` in `Program.cs` at startup

---

### Step 4 — AppHost wiring

**What**: Configure `Taskify.AppHost/Program.cs` to wire PostgreSQL → Api → Web.
**References**:
- [research.md R-012 §5](research.md) — **Critical naming rule**: `AddDatabase("taskifydb")` name must match `AddNpgsqlDbContext<T>("taskifydb")` exactly
- [research.md R-010 §2](research.md) — `WithReference` + `WaitFor` pattern

**Key actions**:
```csharp
var postgres = builder.AddPostgres("postgres").AddDatabase("taskifydb");
var api = builder.AddProject<Projects.Taskify_Api>("taskify-api")
    .WithReference(postgres).WaitFor(postgres);
builder.AddProject<Projects.Taskify_Web>("taskify-web")
    .WithReference(api).WaitFor(api);
```

---

### Step 5 — REST controllers

**What**: Implement all five controllers with full CRUD per the REST contract.
**References**:
- [contracts/rest-api.md](contracts/rest-api.md) — every endpoint's method, route, request shape, response shape, and status codes

| Controller | Endpoints | Key Notes |
|---|---|---|
| `UsersController` | `GET /api/users`, `GET /api/users/{id}` | Read-only; no create in Phase 1 |
| `ProjectsController` | `GET /api/projects`, `GET /api/projects/{id}` | Include task count |
| `TasksController` | `GET`, `POST`, `PUT /api/tasks/{id}`, `DELETE /api/tasks/{id}`, `PATCH /api/tasks/{id}/status`, `PATCH /api/tasks/{id}/assignee` | LWW on status/assignee |
| `CommentsController` | `GET /api/tasks/{id}/comments`, `POST`, `DELETE /api/tasks/{taskId}/comments/{id}` | 403 if not owner |
| `NotificationsController` | `GET /api/users/{id}/notifications`, `PUT /api/users/{id}/notifications/{nId}/read` | Mark-read only |

**TDD gate**: write failing integration tests (Testcontainers) for each endpoint **before** implementing the controller.

---

### Step 6 — Service layer (TaskService + CommentService)

**What**: Business logic for task updates (LWW) and comment deletion (ownership check).
**References**:
- [research.md R-013](research.md) — `ExecuteUpdateAsync` LWW pattern; optimistic concurrency
- [contracts/signalr-hub.md](contracts/signalr-hub.md) — after mutating tasks, broadcast via `IHubContext<TaskifyHub>`

**Key patterns**:
```csharp
// LWW status update (TaskService)
await _db.Tasks
    .Where(t => t.Id == id && t.UpdatedAt < dto.UpdatedAt)
    .ExecuteUpdateAsync(s => s
        .SetProperty(t => t.Status, dto.Status)
        .SetProperty(t => t.UpdatedAt, dto.UpdatedAt));
```

---

### Step 7 — TaskifyHub

**What**: Implement SignalR hub; define all server→client events.
**References**:
- [contracts/signalr-hub.md → Hub Methods and Client Events tables](contracts/signalr-hub.md) — all method signatures
- [research.md R-003](research.md) — hub registration, `AddSignalR()`, CORS for SignalR

**Key actions**:
1. Create `Taskify.Api/Hubs/TaskifyHub.cs` implementing hub methods from contract
2. Register with `builder.Services.AddSignalR()`
3. Map at `/hubs/taskify`
4. Inject `IHubContext<TaskifyHub>` into `TaskService` and `CommentService` for push after writes

---

### Step 8 — Typed HttpClient in Web

**What**: Configure `ApiClient` as a typed HttpClient that uses Aspire service discovery.
**References**:
- [research.md R-010 §6](research.md) — `AddHttpClient<ApiClient>` + `AddServiceDiscovery()` pattern
- [research.md R-010 §4](research.md) — `UseServiceDiscovery()` extension on `HttpClientBuilder`
- [research.md R-010 §5](research.md) — base-address pattern `https+http://taskify-api`

**Key setup** (in `Taskify.Web/Program.cs`):
```csharp
builder.Services.AddHttpClient<ApiClient>(c =>
    c.BaseAddress = new Uri("https+http://taskify-api"))
    .AddServiceDiscovery();
```

---

### Step 9 — BoardHubClient (SignalR in Blazor Server)

**What**: Implement `BoardHubClient` — the `HubConnection` in the Blazor Server frontend.
**References**:
- [research.md R-011 §1 — §8](research.md) — **all subsections apply**:
  - §1: Why `IHttpMessageHandlerFactory` bridge is **required** on Blazor Server
  - §2: Package requirements
  - §3: `HubConnectionBuilder` configuration with service discovery
  - §4: Registering the client in DI
  - §5: Subscribing to server→client events
  - §6: Invoking hub methods from Blazor
  - §7: Lifecycle management (`DisposeAsync`)
  - §8: Connection retry policy

**⚠ Critical**: Standard `HubConnectionBuilder.WithUrl()` will NOT resolve Aspire service names on Blazor Server. Must use `IHttpMessageHandlerFactory` bridge as documented in R-011 §1.

---

### Step 10 — IdentityService

**What**: Scoped service that stores the currently selected user; no auth middleware.
**References**:
- [research.md R-005](research.md) — Blazor Server scoped service lifetime; circuit-lifetime storage

**Key actions**:
1. Register `IdentityService` as `Scoped` in `Taskify.Web/Program.cs`
2. `UserSelection.razor` sets `IdentityService.CurrentUser` on click
3. All other pages/services inject `IdentityService` for the current user context

---

### Step 11 — Design tokens + accessibility

**What**: Define CSS custom properties (design tokens) and wire axe-core for WCAG 2.1 AA.
**References**:
- [quickstart.md → Design Tokens section](quickstart.md) — exact token names and values
- [research.md R-007](research.md) — axe-core integration in CI; colour contrast requirements

**Key actions**:
1. Add token declarations to `wwwroot/css/app.css` as `--taskify-*` CSS variables
2. Reference tokens in all component styles (no hardcoded colours)
3. Add `axe-core` to Playwright CI step; configure contrast + ARIA checks

---

### Step 12 — Blazor pages and components

Implement each UI component in dependency order. Each component must have bUnit tests **before** it is marked complete (TDD gate).

#### 12a — `UserSelection.razor`
**References**: [spec.md US-001](spec.md); [research.md R-014](research.md) — bUnit 2.x event dispatch

#### 12b — `ProjectList.razor`
**References**: [spec.md US-002](spec.md); [research.md R-014](research.md)

#### 12c — `TaskCard.razor`
**References**: [spec.md US-003](spec.md); [research.md R-014](research.md); [research.md R-010b §4](research.md) — SortableJS `data-task-id` attribute

#### 12d — `KanbanBoard.razor`
**References**: [spec.md US-003, US-004](spec.md); [research.md R-014](research.md); [research.md R-010b §6, §7](research.md) — column layout; drag-and-drop event handler

#### 12e — `TaskDetail.razor`
**References**: [spec.md US-005](spec.md); [research.md R-014](research.md)

#### 12f — `CommentItem.razor`
**References**: [spec.md US-006](spec.md); [research.md R-014](research.md) — conditional delete button (owner only)

---

### Step 13 — SortableJS shim + IAsyncDisposable

**What**: Implement the ES module JS interop shim for drag-and-drop. Strict 3-step disposal.
**References**:
- [research.md R-010b §4](research.md) — `DotNetObjectReference<T>` creation
- [research.md R-010b §5](research.md) — `[JSInvokable] OnTaskDropped(string taskId, string fromCol, string toCol)` signature (positional args — do NOT use named args from JS)
- [research.md R-010b §6](research.md) — ES module `import()` + `Setup` export pattern
- [research.md R-010b §7](research.md) — **Strict disposal order**: (1) call `sortable.destroy()` JS-side, (2) `IJSObjectReference.DisposeAsync()`, (3) `DotNetObjectReference.Dispose()`

**Key file**: `wwwroot/js/sortable-interop.js` — must be loaded as ES module (`import()` not `<script src>`)

---

### Step 14 — SignalR wire-up in KanbanBoard

**What**: Subscribe `KanbanBoard.razor` to `BoardHubClient` events; re-render on push.
**References**:
- [research.md R-011 §6](research.md) — `InvokeAsync(StateHasChanged)` required (Blazor Server threading)
- [research.md R-011 §7](research.md) — unsubscribe on `DisposeAsync` to prevent memory leaks

**⚠ Warning**: Never call `StateHasChanged()` directly from a SignalR callback — always use `InvokeAsync(StateHasChanged)` to marshal to the Blazor render thread.

---

### Step 15 — bUnit 2.x component tests

**What**: Full component test coverage for all six Razor components.
**References**:
- [research.md R-014](research.md) — complete bUnit 2.x API reference

| bUnit 2.x API | Replaces (1.x) | Notes |
|---|---|---|
| `BunitContext` | `TestContext` | Base class for all test classes |
| `ctx.Render<T>()` | `ctx.RenderComponent<T>()` | Returns `IRenderedComponent<T>` |
| `ctx.SetupModule(url, ...)` | Manual interop mock | Mock ES modules (SortableJS) |
| `cut.VerifyInvoke(...)` | Custom assertions | Verify JS interop calls |
| `ctx.DisposeComponentsAsync()` | Manual dispose | Async teardown for IAsyncDisposable |

---

### Step 16 — API integration tests

**What**: Integration tests for all REST endpoints using Testcontainers.PostgreSQL (real DB).
**References**:
- [contracts/rest-api.md](contracts/rest-api.md) — expected response shapes and status codes
- [research.md R-012 §6](research.md) — `WebApplicationFactory` + Testcontainers pattern

| Scenario | Endpoint | Assert |
|---|---|---|
| List all users | `GET /api/users` | 200, returns 5 users |
| Move task to Done | `PATCH /api/tasks/{id}/status` | 200, LWW applied |
| Delete own comment | `DELETE …/comments/{id}` | 204 |
| Delete another's comment | `DELETE …/comments/{id}` | 403 |
| Mark notification read | `PUT …/notifications/{id}/read` | 200 |
| SignalR: join board | Hub method `JoinBoard` | `BoardUpdated` event received |

---

### Step 17 — Performance benchmarks + CI gates

**What**: BenchmarkDotNet baselines; k6 load test; add coverage + perf gates to CI.
**References**:
- [research.md R-008](research.md) — BenchmarkDotNet setup; k6 threshold config

**CI gates**:
- Coverlet: `--threshold 80 --threshold-type line` (overall), custom 95% for `Services/` folder
- BenchmarkDotNet: run in CI with `--filter *` — fail build if any benchmark regresses > 10%
- k6: `p(95) < 200` for GET endpoints; `p(95) < 500` for POST/PUT/PATCH

---

### Step 18 — Code quality enforcement

**What**: Enforce formatting and static analysis across all projects.
**References**:
- [research.md R-012 §1](research.md) — `Directory.Build.props` shared NuGet properties

**Actions**:
1. Add `.editorconfig` with CSharpier formatting rules
2. Add `SonarAnalyzer.CSharp` to `Directory.Build.props` (applies to all projects)
3. Add `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` to `Directory.Build.props`
4. CI step: `dotnet csharpier --check .` — fail on unformatted code

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| SortableJS JS interop (ES module shim + 3-step disposal) | Blazor Server has no native drag-and-drop API; SortableJS is the spec-mandated library | A CSS-only drag-and-drop would not provide column-drop callbacks; Blazor's built-in drag events do not give drop-index context needed for task reordering |
| 5-project solution (AppHost + ServiceDefaults + Shared + Api + Web) | .NET Aspire requires AppHost and ServiceDefaults as separate projects; Shared is needed to avoid circular DTO references between Api and Web | A single-project monolith would couple Blazor UI to EF Core entities and prevent independent scaling/testing of the API |
