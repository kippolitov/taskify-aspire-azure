# Taskify

A team productivity platform with Kanban-style task boards, built as a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) application and deployed to Azure Container Apps. Taskify was developed using a spec-driven workflow ([GitHub Spec Kit](https://github.com/github/spec-kit)) — the full specifications, plans, and tasks live under [`specs/`](specs/).


## About this project

Taskify is a real-time Kanban collaboration platform built end-to-end as a portfolio project, covering system design, AI-assisted development workflow, cloud infrastructure, and observability.

---

### Application

A team productivity app: five predefined users, three sample projects, a four-column Kanban board (To Do → In Progress → In Review → Done), drag-and-drop task management, and live multi-user board updates.

**Stack:** .NET 10 · ASP.NET Core · Blazor · Entity Framework Core · PostgreSQL · SignalR · .NET Aspire · Azure Container Apps · Bicep · azd

---

### Architecture

The solution is composed of six projects orchestrated by **.NET Aspire**:

| Project | Role |
|---|---|
| `Taskify.AppHost` | Aspire orchestrator — wires services, injects connection strings, manages startup order |
| `Taskify.Api` | ASP.NET Core REST API + SignalR hub (`TaskifyHub`) |
| `Taskify.Web` | Blazor Server front end |
| `Taskify.Migrator` | One-shot EF Core migration runner |
| `Taskify.Shared` | DTOs and enums shared across service boundary |
| `Taskify.ServiceDefaults` | OpenTelemetry, health checks, resilience — one call, all projects |

Real-time board updates use SignalR groups keyed by project ID. When any client moves a task, the API broadcasts to every connected board viewer — no polling.

The AppHost composition graph expresses startup dependencies declaratively:

```csharp
var migrate = builder.AddProject<Taskify_Migrator>("taskify-migrate")
    .WithReference(postgres).WaitFor(postgres);

var api = builder.AddProject<Taskify_Api>("taskify-api")
    .WithReference(postgres).WaitFor(postgres).WaitForCompletion(migrate);

builder.AddProject<Taskify_Web>("taskify-web")
    .WithReference(api).WaitFor(api).WaitForCompletion(migrate);
```

---

### AI-assisted development workflow

The entire build followed a **spec-driven workflow** using [GitHub Spec Kit](https://github.com/github/spec-kit) — a structured sequence of AI agent skills running inside VS Code Copilot:

```
/speckit-specify   →  user stories with acceptance criteria (specs/*/spec.md)
/speckit-clarify   →  targeted Q&A to resolve underspecified areas
/speckit-analyze   →  cross-artifact consistency check (spec, plan, tasks)
/speckit-plan      →  technical design, performance targets, constitution check
/speckit-tasks     →  dependency-ordered implementation tasks (tasks.md)
/speckit-implement →  executes tasks with TDD + automated code review per commit
```

Each `implement` session started from a clean context window — only the ticket and spec loaded — keeping the model inside its reasoning window and preventing stale-context hallucinations. All specs, plans, and task files live under [`specs/`](specs/) and are the primary record of design decisions.

`CONTEXT.md` is a living domain glossary maintained throughout the process: "Status" not "state", "Active User" not "logged-in user", "Comment Author" not "owner". Precise language at the domain layer prevents terminology drift across a long build.

---

### DevOps & CI/CD

Infrastructure is defined as **Bicep** under [`infra/`](infra/) and deployed with the **Azure Developer CLI**:

```bash
azd up   # provisions all Azure resources and deploys both containers
```

Resources provisioned: Azure Container Apps environment, PostgreSQL Flexible Server, Key Vault, Application Insights, Log Analytics Workspace, Container Registry.

The CI/CD pipeline uses **OIDC federation** — no service principal secrets are stored anywhere. GitHub Actions authenticates to Azure via a federated identity credential tied to the repository and branch. The pipeline:

1. Runs the full test suite — xUnit + bUnit + **Testcontainers** (real PostgreSQL in Docker; no mocked database layer)
2. Builds and pushes Docker images via `azd`
3. Rolls out to Azure Container Apps
4. Runs smoke tests against the live health endpoints

Container Apps is configured **scale-to-zero** in the dev environment — near-zero cost when idle, under 5 s cold start.

---

### Observability

OpenTelemetry is wired in from day one through `Taskify.ServiceDefaults`:

- **Traces** — distributed across Blazor → API → EF Core → PostgreSQL, correlated by trace ID
- **Metrics** — ASP.NET Core + HttpClient instrumentation
- **Logs** — structured, scoped, exported via OTLP

Locally, all telemetry flows into the **.NET Aspire dashboard** — no extra infrastructure. In Azure, the same OTLP pipeline routes to **Application Insights** with no instrumentation changes between environments.

Every service exposes `/health/live` and `/health/ready` via `MapDefaultEndpoints()`, consumed by Container Apps readiness and liveness probes.

---


## Features

- Kanban board with standard columns (To Do, In Progress, In Review, Done)
- Five predefined users (one product manager, four engineers) and three sample projects — no login required
- Task cards with assignment, unlimited comments, and drag-and-drop
- Real-time board updates across clients via SignalR
- Notifications for task and comment activity

## Architecture

| Project | Description |
|---|---|
| `Taskify.AppHost` | .NET Aspire orchestrator (local dev composition) |
| `Taskify.Api` | ASP.NET Core Web API, EF Core, SignalR hub |
| `Taskify.Web` | Blazor front end |
| `Taskify.Migrator` | Database migration runner |
| `Taskify.Shared` | Shared DTOs and enums |
| `Taskify.ServiceDefaults` | Aspire service defaults (telemetry, health, resilience) |

**Stack:** .NET 10 / C# · ASP.NET Core · Blazor · Entity Framework Core · PostgreSQL · SignalR · .NET Aspire · Azure Container Apps · Bicep · Azure Developer CLI (azd).

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (for the local PostgreSQL container managed by Aspire)

### Run locally

```bash
dotnet run --project src/Taskify.AppHost
```

The Aspire dashboard launches and orchestrates the API, web front end, and a PostgreSQL container. Connection strings for local development are wired up by Aspire — no manual configuration needed.

### Run tests

```bash
dotnet test
```

## Deployment

Infrastructure is defined as Bicep under [`infra/`](infra/) and deployed with the Azure Developer CLI:

```bash
azd up
```

See [`docs/azure-deployment.md`](docs/azure-deployment.md) and [`docs/github-actions-setup.md`](docs/github-actions-setup.md) for CI/CD and environment setup. Secrets (database passwords, Azure credentials) are supplied at deploy time via GitHub Actions secrets and Azure OIDC — none are stored in the repository.

---

## License

[MIT](LICENSE)
