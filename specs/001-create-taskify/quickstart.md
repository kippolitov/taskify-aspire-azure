# Quickstart: Create Taskify

**Feature**: `001-create-taskify`  
**Date**: March 5, 2026  
**Plan**: [plan.md](plan.md) | **Data Model**: [data-model.md](data-model.md) | **API Contract**: [contracts/rest-api.md](contracts/rest-api.md)

---

## Prerequisites

| Tool | Version | Install |
|---|---|---|
| .NET SDK | 10.0 or later | https://dot.net/download |
| .NET Aspire workload | 10.x | `dotnet workload install aspire` |
| Docker Desktop | 4.x or later | https://www.docker.com/products/docker-desktop/ |
| PowerShell | 7.x (macOS/Linux) | `brew install powershell` |
| Node.js (optional, for benchmarks) | 20 LTS | https://nodejs.org |

> Docker is required to run the PostgreSQL container provisioned by .NET Aspire.

---

## First-Time Setup

```bash
# 1. Clone the repository (skip if already cloned)
git clone <repo-url> taskify
cd taskify

# 2. Check out the feature branch
git checkout 001-create-taskify

# 3. Restore .NET tools and NuGet packages
dotnet restore

# 4. Confirm Aspire workload is installed
dotnet workload list   # should show: aspire
# If missing:
dotnet workload install aspire
```

---

## Run the Application (Development)

.NET Aspire's AppHost orchestrates all services and spins up a PostgreSQL container automatically.

```bash
# From the repo root
dotnet run --project src/Taskify.AppHost

# Aspire dashboard opens at: https://localhost:15000  (default)
# Blazor Web app is reachable via the dashboard's "Taskify.Web" endpoint link
# The API is reachable via the dashboard's "Taskify.Api" endpoint link
```

On first run, the database is created and seeded automatically with:
- 5 predefined users
- 3 sample projects
- 10 task cards distributed across columns

> **No manual database setup is required.**

---

## Access Taskify in the Browser

1. Open the Blazor Web URL shown in the Aspire dashboard (typically `http://localhost:5XXX`).
2. **Landing screen**: Click your name from the list of five users.
3. **Project list**: Click any project card to open its Kanban board.
4. **Kanban board**: Drag cards between columns; open a card to assign a user or add comments.

---

## Running Tests

### All tests

```bash
dotnet test
```

### Unit tests only

```bash
dotnet test tests/Taskify.Api.Tests --filter Category=Unit
dotnet test tests/Taskify.Web.Tests
```

### Integration tests (requires Docker)

```bash
dotnet test tests/Taskify.Integration.Tests
```

Integration tests use `Testcontainers.PostgreSQL` to spin up an isolated database per test run. Docker must be running.

### Coverage report

```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage-report -reporttypes:Html
open coverage-report/index.html   # macOS
```

Coverage thresholds (enforced in CI):
- Overall line coverage: **≥ 80%**
- Critical paths (task mutation, data persistence, comment ownership): **≥ 95%**

---

## Design Reference

### Color tokens (defined in `src/Taskify.Web/wwwroot/css/tokens.css`)

| Token | Value | Usage |
|---|---|---|
| `--color-card-mine` | `#E0F2FE` | Background for cards assigned to the active user |
| `--color-card-default` | `#FFFFFF` | Background for all other cards |
| `--color-primary` | `#0EA5E9` | Primary buttons and active states |
| `--color-text` | `#0F172A` | Primary body text |
| `--color-text-muted` | `#64748B` | Secondary labels, timestamps |
| `--spacing-4` | `1rem` | Base spacing unit |

All contrast ratios against text colors pass **WCAG 2.1 AA (4.5:1)**.

### Column order (fixed)

| Index | Status value | Display label |
|---|---|---|
| 0 | `ToDo` | To Do |
| 1 | `InProgress` | In Progress |
| 2 | `InReview` | In Review |
| 3 | `Done` | Done |

---

## Key Architecture Decisions

| Decision | Choice | See |
|---|---|---|
| Orchestration | .NET Aspire AppHost | [research.md R-001](research.md#r-001) |
| Drag-and-drop | SortableJS via JS interop (drop-only callback) | [research.md R-002](research.md#r-002) |
| Real-time updates | SignalR `TaskifyHub`, board-scoped groups | [research.md R-003](research.md#r-003) |
| Session identity | Scoped `IdentityService` (no persistence on refresh) | [research.md R-005](research.md#r-005) |
| Accessibility | axe-core (runtime, dev builds); WCAG 2.1 AA target | [research.md R-007](research.md#r-007) |

---

## Useful Commands

| Task | Command |
|---|---|
| Run all services | `dotnet run --project src/Taskify.AppHost` |
| Apply EF migrations | `dotnet ef database update --project src/Taskify.Api` |
| Add a new EF migration | `dotnet ef migrations add <Name> --project src/Taskify.Api` |
| Run linter (CSharpier) | `dotnet csharpier --check .` |
| Format all files | `dotnet csharpier .` |
| Run benchmarks | `dotnet run --project tests/Taskify.Benchmarks -c Release` |

---

## CI Pipeline (GitHub Actions — Phase 1)

```
Trigger: push to 001-create-taskify, PR to main

Steps:
  1. dotnet restore
  2. dotnet csharpier --check .          # code formatting gate
  3. dotnet build --no-restore
  4. dotnet test (unit + contract)        # fast tests, no Docker
  5. dotnet test (integration)            # requires Docker service container
  6. dotnet test --collect coverage       # coverage threshold enforcement
  7. k6 run .specify/benchmarks/api.js   # performance regression check
```

---

## Troubleshooting

| Problem | Solution |
|---|---|
| Docker container fails to start | Ensure Docker Desktop is running and the `5432` port is not already in use |
| Aspire dashboard not reachable | Check that step output shows the HTTPS developer certificate is trusted: `dotnet dev-certs https --trust` |
| EF migration error on startup | Run `dotnet ef database update --project src/Taskify.Api` manually and re-start |
| SortableJS drag-and-drop not working | Open browser DevTools > Console and check for JS errors; confirm `sortable-interop.js` loads with no 404 |
| Card color not updating after re-assign | Confirm `TaskAssigned` SignalR event is being received; check `BoardHubClient` subscription in DevTools > Network > WS |
