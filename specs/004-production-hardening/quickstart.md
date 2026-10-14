# Quickstart: Validating Production Hardening

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Contracts**: [rest-api.md](contracts/rest-api.md), [signalr-hub.md](contracts/signalr-hub.md)

This guide proves each user story end to end. Run the stories in order; each section lists what must be true before the story is accepted.

## Prerequisites

- .NET 10 SDK (`dotnet --version` reports 10.0.x)
- Docker running. The tests start PostgreSQL 16 and Redis containers.
- Local tools: `dotnet tool restore` (CSharpier, ReportGenerator)
- The local signing key, from P3 onward: `dotnet user-secrets set "Parameters:auth-signing-key" "<at least 32 random bytes, base64>" --project src/Taskify.AppHost`
- For the Azure checks: Azure CLI and `azd` signed in to the dev subscription

## P1 – Trustworthy verification

| # | Run | Expected |
|---|---|---|
| 1 | `dotnet test Taskify.slnx --settings coverage.runsettings` | All tests pass. The log shows one `postgres:16` container started for the whole run. |
| 2 | `grep -rn "InMemory" tests/ --include=*.csproj` | No matches (FR-002). |
| 3 | `grep -rn "MigrateAsync" src/` | Matches only in `src/Taskify.Migrator/` (FR-006). |
| 4 | `git ls-files src/Taskify.Api/Migrations` | The same three files as before: no new migration ([data-model.md](data-model.md)). |
| 5 | `docker build -f src/Taskify.Api/Dockerfile .` and `docker build -f src/Taskify.Web/Dockerfile .` | Both succeed (FR-007). |
| 6 | `dotnet run --project src/Taskify.AppHost`, then open the dashboard | `taskify-migrate` reaches **Finished** before `taskify-api` and `taskify-web` start. |
| 7 | Stop PostgreSQL in the dashboard, then restart the AppHost with PostgreSQL unavailable | `taskify-migrate` fails, and `taskify-api` and `taskify-web` stay **Waiting** (scenario 1.4). |

**Prove the gates fail.** SC-002 requires four deliberate breaks. Do each on a throwaway branch, open a PR, and confirm the pipeline turns red:

1. Delete the `COPY Directory.Packages.props` line from one Dockerfile.
2. Add 200 untested lines to an API service, so coverage drops below 80%.
3. Remove `[Required]` from `CreateTaskRequest.Title`, so the validation test fails.
4. Put `MigrateAsync` back into API startup, so `SchemaOwnershipTests` fails.

## P2 – Consistent, self-describing API

| # | Run | Expected |
|---|---|---|
| 1 | `dotnet test --filter "Category=Contract"` | The OpenAPI test passes: every operation lists a 2xx response and at least one 4xx or 5xx response (SC-003). |
| 2 | Run the AppHost, then open `{api}/openapi/v1.json` | `openapi` is `3.1.x`. `PUT /api/tasks/{id}` and `PATCH /api/tasks/{id}/assignee` are separate operations. No `requestingUserId` or `authorId` appears. |
| 3 | `PATCH {api}/api/tasks/1/status` with `{ "status": "Archived" }`, then with `{ "status": 7 }` | `400`, `application/problem+json`, and the Task is unchanged. |
| 4 | `GET {api}/api/tasks/99999` | `404` Problem Details, in the same shape as every other not-found. |
| 5 | `dotnet test --filter "FullyQualifiedName~TaskListQuery"` | The SQL for the task list contains a `count` subquery and no comment columns (FR-014). |
| 6 | `dotnet run -c Release --project tests/Taskify.Benchmarks -- --filter "*TaskList*"` | The task list for 500 Tasks × 20 Comments has p95 ≤ 200 ms (SC-004). |
| 7 | `grep -rnE '"(TaskMoved\|TaskAssigned\|TaskCreated\|CommentAdded\|CommentEdited\|CommentDeleted)"' src/` | No matches. Message names appear only through `nameof` (FR-018). |
| 8 | Use the board: drag a card, assign it, comment, edit and delete the comment, in two browser windows | Identical to before; the other window updates live (FR-041, SC-012). |

## P3 – Server-vouched identity

Run the AppHost and note the API address from the dashboard. Every request below goes directly to the API, outside the web app.

| # | Request | Expected |
|---|---|---|
| 1 | `GET /api/users`, with no token | `200` and five Users (the landing screen still works). |
| 2 | `POST /api/auth/token` with `{ "userId": 2 }` | `200` with `accessToken` and `expiresAt` about 15 minutes ahead. Save the token as `$T2`. |
| 3 | `POST /api/auth/token` with `{ "userId": 999 }` | `404`, and no token (scenario 3.7). |
| 4 | `PATCH /api/tasks/1/status`, with no token | `401`, and the Task is unchanged (scenario 3.3). |
| 5 | `POST /api/tasks/1/comments` with `$T2` and `{ "text": "hi", "authorId": 1 }` | `201`, with `author.id` = **2**. The body's `authorId` is ignored (scenario 3.5). |
| 6 | Issue `$T3` for User 3, then `PUT /api/comments/{id from step 5}` with `$T3` | `403`, and the Comment is unchanged (scenario 3.4). |
| 7 | `DELETE` the same Comment with `$T3` | `403`. With `$T2`, it returns `204`. |
| 8 | Connect a hub client with no token | Refused with 401 (scenario 3.6). |
| 9 | Pick a User in the web app and use the board for more than 15 minutes | No sign-in prompt; live updates continue (silent renewal, FR-029). |

The automated equivalents run in CI under `tests/Taskify.Api.Tests/Identity/`.

## P4 – Safe to run at scale

### Locally, automated

| # | Run | Expected |
|---|---|---|
| 1 | `dotnet test --filter "FullyQualifiedName~MultiInstance"` | Two API hosts share PostgreSQL and Redis. A move through host B reaches a hub client on host A within 2 s in 20 of 20 iterations (SC-006). |
| 2 | `dotnet test --filter "FullyQualifiedName~Resilience"` | A failing `POST` is sent exactly once; a failing `GET` is sent more than once (SC-008). |
| 3 | `dotnet test --filter "FullyQualifiedName~TaskMoveMetric"` | N moves increase `taskify.tasks.moved` by exactly N (SC-010). |
| 4 | Run the AppHost and open the dashboard | A `redis` resource is running; `taskify-api` references it. |

### In Azure dev, after `azd up` or the deploy workflow

| # | Check | Expected |
|---|---|---|
| 1 | `az deployment group what-if` on the change | The API has `external: false`. The web app has `stickySessions.affinity: sticky` and `activeRevisionsMode: Single`. Both apps have startup, liveness and readiness probes. A Managed Redis B0 resource is added. |
| 2 | `curl -s -o /dev/null -w "%{http_code}" https://<api-internal-fqdn>/health` from your machine | Not `200`; the environment proxy rejects it (FR-038). |
| 3 | Open the web app and pick a User | The board works; the web app reaches the API internally. |
| 4 | In dev only, point the API's liveness probe at `/does-not-exist` with `az containerapp update`, watch the revision for 2 minutes, then revert | Replicas fail liveness and are restarted automatically within 2 minutes (SC-007). |
| 5 | Move a Task, then open Application Insights, Transaction search | Within 5 minutes, one end-to-end transaction shows `taskify-web` calling `taskify-api` (SC-009). |
| 6 | In Application Insights, Metrics, open custom metric `taskify.tasks.moved` | It increases with each move (FR-039). |
| 7 | Scale the web app to 2 replicas and use the board in one browser for 5 minutes | No reconnect dialog; the session stays on one replica (FR-033). |

## Closing check for every story

- The CI pipeline is green on the PR, including tests, the coverage gate, image builds, `dotnet format` and the Bicep build.
- Read the README line by line against the repository and the deployed system: there are no false claims about testing, monitoring, health checks or Notifications (SC-011).
- Every acceptance scenario in [specs/001-create-taskify/spec.md](../001-create-taskify/spec.md) still passes (SC-012).
