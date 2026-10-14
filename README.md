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
| `Taskify.Shared` | The contract between API and web app: DTOs, request records, real-time messages and the single JSON configuration |
| `Taskify.ServiceDefaults` | OpenTelemetry, health checks, resilience — one call, all projects |

Real-time board updates use SignalR groups keyed by project ID. When any client moves a task, the API broadcasts to every connected board viewer — no polling.

The AppHost composition graph expresses startup dependencies declaratively:

```csharp
var migrate = builder.AddProject<Taskify_Migrator>("taskify-migrate")
    .WithReference(postgres).WaitFor(postgres);

var api = builder.AddProject<Taskify_Api>("taskify-api")
    .WithReference(postgres).WithReference(redis)
    .WaitFor(postgres).WaitFor(redis).WaitForCompletion(migrate);

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

Resources provisioned: Azure Container Apps environment, PostgreSQL Flexible Server, Azure Managed Redis (the live-update backplane between API replicas), Application Insights, Log Analytics Workspace, Container Registry. The API is internal to the Container Apps environment; only the web app is public.

The CI/CD pipeline uses **OIDC federation** — no service principal secrets are stored anywhere. GitHub Actions authenticates to Azure via a federated identity credential tied to the repository and branch. The pipeline:

1. Runs the test suite and checks the build — the API tests call the API through its full HTTP pipeline against PostgreSQL 16 in Docker (Testcontainers); bUnit tests cover the Blazor components. The pipeline enforces an 80% overall and a 95% critical-path line-coverage gate, and builds both container images
2. Builds and pushes Docker images via `azd`
3. Rolls out to Azure Container Apps
4. Runs smoke tests through the web app: its home page must list the seeded users, its `/health` must return 200, and the API must not be reachable from outside

In dev, both apps scale to zero when idle. In prod, the web app keeps at least one replica, because a Blazor Server session lives in one replica; sticky sessions keep each browser on its replica.

---

### Observability

OpenTelemetry is wired in from day one through `Taskify.ServiceDefaults`:

- **Traces** — distributed across Blazor → API → EF Core → PostgreSQL, correlated by trace ID
- **Metrics** — ASP.NET Core + HttpClient instrumentation, plus `taskify.tasks.moved` (one per Task Status change) and `taskify.broadcasts.failed` on the `Taskify.Api` meter
- **Logs** — structured, scoped, source-generated (`[LoggerMessage]`)

Locally, all telemetry flows into the **.NET Aspire dashboard** — no extra infrastructure. In Azure, the Azure Monitor OpenTelemetry distro sends the same traces, metrics and logs to **Application Insights**; it switches on when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.

Every service exposes `/alive` (liveness: the process responds) and `/health` (readiness: includes the database check in the API) via `MapDefaultEndpoints()`. Container Apps uses `/alive` for the startup and liveness probes and `/health` for the readiness probe, and restarts a replica that stops responding.

---


## Features

- Kanban board with standard columns (To Do, In Progress, In Review, Done)
- Five predefined users (one product manager, four engineers) and three sample projects — no login required; the server issues and checks a short-lived identity for the selected user, so no request can act as someone else
- Task cards with assignment, unlimited comments, and drag-and-drop
- Real-time board updates across clients via SignalR, across every API replica

## Architecture

| Project | Description |
|---|---|
| `Taskify.AppHost` | .NET Aspire orchestrator (local dev composition) |
| `Taskify.Api` | ASP.NET Core Web API, EF Core, SignalR hub |
| `Taskify.Web` | Blazor front end |
| `Taskify.Migrator` | Database migration runner |
| `Taskify.Shared` | Shared contracts: DTOs, requests, real-time messages, JSON settings |
| `Taskify.ServiceDefaults` | Aspire service defaults (telemetry, health, resilience) |

**Stack:** .NET 10 / C# · ASP.NET Core · Blazor · Entity Framework Core · PostgreSQL · SignalR · .NET Aspire · Azure Container Apps · Bicep · Azure Developer CLI (azd).

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- Docker (for the local PostgreSQL and Redis containers managed by Aspire)

### Run locally

```bash
dotnet run --project src/Taskify.AppHost
```

The Aspire dashboard launches and orchestrates the API, web front end, and PostgreSQL and Redis containers. Connection strings for local development are wired up by Aspire.

Before the first run, set the key that signs the selected user's identity (at least 32 random bytes, base64-encoded). It is stored in your user secrets, never in the repository:

```bash
dotnet user-secrets set "Parameters:auth-signing-key" "$(openssl rand -base64 32)" --project src/Taskify.AppHost
```

### Run tests

Docker must be running: the API tests start PostgreSQL 16 and Redis 7 containers through Testcontainers.

```bash
dotnet test
```

## Deployment

Infrastructure is defined as Bicep under [`infra/`](infra/) and deployed with the Azure Developer CLI:

```bash
azd up
```

See [`docs/azure-deployment.md`](docs/azure-deployment.md) and [`docs/github-actions-setup.md`](docs/github-actions-setup.md) for CI/CD and environment setup. Secrets (the database password, the identity signing key, Azure credentials) are supplied at deploy time via GitHub Actions secrets and Azure OIDC — none are stored in the repository.

---

## License

[MIT](LICENSE)
