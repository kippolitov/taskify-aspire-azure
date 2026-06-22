# Taskify

A team productivity platform with Kanban-style task boards, built as a [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/) application and deployed to Azure Container Apps. Taskify was developed using a spec-driven workflow ([GitHub Spec Kit](https://github.com/github/spec-kit)) — the full specifications, plans, and tasks live under [`specs/`](specs/).

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

## License

[MIT](LICENSE)
