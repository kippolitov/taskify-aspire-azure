# Taskify hardening and modernization plan

**Status:** proposal, 2026-10-11. Input for a Spec Kit session (`/speckit.specify`, then `/speckit.plan`). It is not a spec: it records what to change, why, and what "done" means, so the spec session does not have to rediscover it.

## Goal

Resolve the 13 weak spots found in the October 2026 code review without changing the stack, and adopt current .NET 10 and C# 14 features where they make the code shorter or clearer.

Every change must pass three tests:

1. **Explainable**: a developer can describe the design in two minutes.
2. **Fewer concepts**: the change removes more special cases than it adds.
3. **No feature for its own sake**: a newer API is used only where it replaces hand-written code.

## Constraints

- **Stack stays**: .NET 10, ASP.NET Core, Blazor Web App in Interactive Server mode, EF Core 10 with Npgsql, SignalR, Aspire 13, Azure Container Apps, Bicep, `azd`, GitHub Actions. The only new runtime dependency is Redis, as the SignalR backplane (decision D3).
- **Constitution applies** (`.specify/memory/constitution.md`): tests first, at least 80% line coverage overall and 95% on critical paths, one integration test per user story, cyclomatic complexity of 10 or less, the performance budgets.
- **Behaviour is unchanged** for the person using the board, except where "Contract changes" says otherwise.
- **Domain language** follows `CONTEXT.md` (Task, Status, Project, Active User, Comment, Author).

## Weak spots and where each is resolved

| # | Weak spot | Evidence | Phase |
|---|---|---|---|
| 1 | No test touches real PostgreSQL; all data tests use the EF Core InMemory provider. `Testcontainers.PostgreSQL` and `Microsoft.AspNetCore.Mvc.Testing` are referenced but unused. | `tests/Taskify.Api.Tests/Helpers/TestHelpers.cs` | 1 |
| 2 | No authentication. The caller states who it is in the request. | `requestingUserId` and `authorId` in `src/Taskify.Api/Controllers/CommentsController.cs` | 3 |
| 3 | Scale-out breaks real-time features: 0 to 10 replicas, no session affinity for Blazor Server, hub groups held in one API process. | `infra/resources/container-apps.bicep` | 4 |
| 4 | Migrations run in two places. | `src/Taskify.Api/Program.cs` lines 57-61 and `src/Taskify.Migrator` | 1 |
| 5 | The API Docker build fails at `dotnet restore` with NU1015: the Dockerfile copies `Directory.Build.props` but not `Directory.Packages.props`. Confirmed with `docker build` on 2026-10-11. The web Dockerfile has the same gap. | `src/Taskify.Api/Dockerfile`, `src/Taskify.Web/Dockerfile` | 1 |
| 6 | The task list loads every comment row to produce a count; the DTO mapping runs in memory. | `src/Taskify.Api/Services/TaskService.cs` lines 15-22 | 2 |
| 7 | No request can be cancelled: no `CancellationToken` anywhere under `src/`. | all controllers and services | 2 |
| 8 | Azure receives `APPLICATIONINSIGHTS_CONNECTION_STRING` but no exporter reads it. | `src/Taskify.ServiceDefaults/Extensions.cs` | 4 |
| 9 | `/health` and `/alive` exist but Container Apps defines no probes. | `infra/resources/container-apps.bicep` | 4 |
| 10 | The standard resilience handler retries every HTTP method, so a failed `POST` can create a duplicate. | `src/Taskify.ServiceDefaults/Extensions.cs` line 25 | 4 |
| 11 | The SignalR contract is written twice by hand: anonymous objects on the server, private records on the client, string method names on both. | `TaskService.cs`, `src/Taskify.Web/Services/BoardHubClient.cs` | 2 |
| 12 | The OpenAPI document has no response schemas: every action returns `IActionResult`. | all controllers | 2 |
| 13 | Expected outcomes travel as tuples with `null!`. | `TaskService.cs` line 45, `CommentService.cs` | 2 |

Also found while reading, and folded into the phases:

- CI collects coverage but enforces no threshold, although `coverage.runsettings` says thresholds are enforced (Phase 1).
- JSON options are configured by hand in three places: MVC, SignalR and `ApiClient` (Phase 2).
- The README still overstates three things: telemetry reaching Application Insights, health endpoints named `/health/live` and `/health/ready` used as probes, and notifications as a feature. Phase 4 makes the first two true; the third needs a wording fix.

## Target design in brief

- **Tests** run the real HTTP pipeline against real PostgreSQL. Unit tests remain only for code with no I/O.
- **The API** is a set of Minimal API endpoint groups returning `TypedResults`. Validation, error bodies and OpenAPI schemas come from the framework, not from code in each action.
- **Contracts** live once in `Taskify.Shared`: request records, response DTOs, hub payloads and the hub client interface. The API, the web app and the tests all compile against them.
- **Services** return `Result<T>` and take a `CancellationToken` and a `TimeProvider`.
- **Identity** is established once, by the API, and read from claims everywhere else.
- **Production** runs more than one replica safely, exports telemetry, and is probed.

## Phase 1: trustworthy tests and builds

Resolves 1, 4, 5. Everything later depends on this, because the constitution requires failing tests first.

Changes:

- Add `public partial class Program;` to the API so tests can host it.
- Create one `WebApplicationFactory<Program>` fixture backed by `Testcontainers.PostgreSQL`. Use PostgreSQL 16, the version in `infra/main.bicep`. Start one container per test run with an xUnit collection fixture, and give each test class its own database, created by the real migrations.
- Rewrite the API tests to call endpoints over HTTP and assert status code, body and database state. Assert hub broadcasts with a real `HubConnection` against the test server.
- Keep plain unit tests only for pure code, such as mapping.
- Remove `Microsoft.EntityFrameworkCore.InMemory` from the test and benchmark projects. Point the benchmarks at the PostgreSQL container.
- Delete the `MigrateAsync` block from `src/Taskify.Api/Program.cs`. `Taskify.Migrator` becomes the only place that changes the schema.
- Copy `Directory.Packages.props` in both Dockerfiles before `dotnet restore`. Add a CI job that builds both images without pushing.
- Run CI tests with `--settings coverage.runsettings` and fail the build under 80% line coverage.
- Optional: one end-to-end smoke test with `Aspire.Hosting.Testing` that starts the AppHost and loads the home page. It needs the Prometheus and Grafana containers to be skippable.

Done when:

- `dotnet test` with Docker running passes, and no project references the InMemory provider.
- A test proves framework behaviour that direct controller calls could not: an invalid status on `PATCH /api/tasks/{id}/status` returns 400 with `application/problem+json`.
- `MigrateAsync` appears only in `Taskify.Migrator`.
- Both images build in CI.
- The README sentence about Testcontainers can be restored, because it is true.

## Phase 2: API and contracts

Resolves 6, 7, 11, 12, 13.

Changes:

- **Minimal APIs.** Replace `Controllers/` with one static class per resource in `src/Taskify.Api/Endpoints/`: `ProjectEndpoints`, `TaskEndpoints`, `CommentEndpoints`, `UserEndpoints`, `NotificationEndpoints`. Each exposes one `MapXxxEndpoints` method. Handlers are private static methods that return `Results<...>` built with `TypedResults`, which gives OpenAPI its response schemas.
- **Validation.** Call `builder.Services.AddValidation()`, new in .NET 10. DataAnnotations on the request records are enforced by the framework. All `ModelState` checks go.
- **Errors.** Call `AddProblemDetails()` and register one `IExceptionHandler` for unexpected failures. Expected outcomes use `Result<T>` with an `ErrorKind` (`NotFound`, `Forbidden`, `Unprocessable`). One method, `ToHttpResult()`, maps a result to a response.
- **Shared contracts.** Move request records into `Taskify.Shared` beside the DTOs. `ApiClient` sends those records instead of anonymous objects.
- **Partial update without a converter.** Delete `UpdateTaskRequestConverter`. See "Contract changes".
- **One JSON setup.** A single method in `Taskify.Shared` configures camelCase and string enums. HTTP JSON options, the SignalR JSON protocol and `ApiClient` all call it.
- **Typed hub.** Declare `ITaskifyClient` and payload records in `Taskify.Shared`. `TaskifyHub` derives from `Hub<ITaskifyClient>`; services inject `IHubContext<TaskifyHub, ITaskifyClient>`. `BoardHubClient` registers handlers with `nameof(ITaskifyClient.TaskMoved)` and receives `ColumnStatus`, not strings.
- **Queries.** Read endpoints project to DTOs in SQL with `AsNoTracking`, using one shared `Expression<Func<TaskItem, TaskDto>>` whose comment count is `t.Comments.Count`. No `Include` for reads.
- **Cancellation.** Every handler takes a `CancellationToken` and passes it through the service into EF Core. `ApiClient` methods accept one. Raise analyzer CA2016 to a warning so the build enforces forwarding.
- **Time.** Register `TimeProvider.System` and inject `TimeProvider` into services. Tests use `FakeTimeProvider`.
- **C# 14, only where it helps.** An `extension` block for properties that read better as members, such as `user.UserId` on `ClaimsPrincipal`. The `field` keyword in `IdentityService` to raise `OnChange` from a property setter. Ordinary extension methods stay ordinary.
- **Blazor.** Add `@key="task.Id"` to the `TaskCard` loop, because SortableJS moves DOM nodes. Use `[PersistentState]`, new in .NET 10, on the lists loaded in `Home` and `ProjectList` so prerendering does not fetch them twice.

Shape to aim for:

```csharp
// src/Taskify.Api/Endpoints/TaskEndpoints.cs
public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api").WithTags("Tasks");
        group.MapGet("/tasks/{id:int}", GetTask);
        group.MapPatch("/tasks/{id:int}/status", MoveTask);
        return group;
    }

    private static async Task<Results<Ok<TaskDto>, ProblemHttpResult>> MoveTask(
        int id, MoveTaskRequest request, TaskService tasks, CancellationToken ct) =>
        (await tasks.MoveAsync(id, request.Status, ct)).ToHttpResult();
}

// src/Taskify.Shared/Realtime/ITaskifyClient.cs
public interface ITaskifyClient
{
    Task TaskMoved(TaskMoved payload);
    Task TaskAssigned(TaskAssigned payload);
    Task TaskCreated(TaskDto task);
    Task CommentAdded(CommentAdded payload);
    Task CommentEdited(CommentEdited payload);
    Task CommentDeleted(CommentDeleted payload);
}

// C# 14 extension property
public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        public int UserId => int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}
```

Done when:

- `Controllers/`, `UpdateTaskRequestConverter` and every `ModelState` check are gone.
- The OpenAPI document lists a typed success response and a problem response for every operation.
- A test captures the SQL for the project task list and shows a count, not comment rows.
- Hub method names appear as string literals nowhere outside `nameof`.
- No service method returns a tuple or `null!`.
- The build fails if an available `CancellationToken` is not forwarded.

## Phase 3: identity

Resolves 2. The product keeps "pick one of five predefined users, no password"; what changes is who vouches for the choice.

Changes:

- Add `POST /api/auth/token`. It takes a user id, checks the user exists, and returns a short-lived JWT signed with a symmetric key.
- Add JWT bearer authentication to the API. Every endpoint group except the token endpoint and the user list calls `RequireAuthorization()`. `TaskifyHub` is marked `[Authorize]`.
- Author and requester come from `user.UserId`. Remove `authorId` and `requestingUserId` from every request.
- In the web app, `IdentityService` obtains and holds the token for the circuit. A `DelegatingHandler` adds the bearer header to `ApiClient`; `BoardHubClient` sets `AccessTokenProvider`.
- The signing key is an Aspire secret parameter locally and a Key Vault secret in Azure.
- Update the definition of Active User in `CONTEXT.md` and record the decision as an ADR under `docs/adr/`.

Stated limit: anyone can still choose any user, because the product has no passwords. The gain is that identity is set in one place, cannot be altered per request, and the API is ready for a real identity provider by configuration.

Done when:

- A mutating call without a token returns 401.
- Editing or deleting another user's comment returns 403, decided from the token.
- No request body, route or query string carries the caller's own user id.
- A hub connection without a token is rejected.

## Phase 4: production readiness

Resolves 3, 8, 9, 10.

Changes:

- **Backplane.** Add Redis to the AppHost and reference it from the API. Add the Redis backplane to SignalR with `Microsoft.AspNetCore.SignalR.StackExchangeRedis`. Add a managed Redis resource to Bicep.
- **Blazor Server affinity.** Enable sticky sessions on the web app's ingress. Keep at least one web replica in production; scale to zero stays for dev.
- **Internal API.** Make the API's ingress internal. The browser never calls it. Run smoke tests through the web app.
- **Probes.** Add startup, liveness (`/alive`) and readiness (`/health`) probes to both container apps.
- **Azure Monitor.** In `Taskify.ServiceDefaults`, call `UseAzureMonitor()` from `Azure.Monitor.OpenTelemetry.AspNetCore` when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set.
- **Safe retries.** Configure the standard resilience handler with `Retry.DisableForUnsafeHttpMethods()`.
- **Own telemetry.** One `Meter` created from `IMeterFactory` with a `taskify.tasks.moved` counter, and `[LoggerMessage]` source-generated logging in the migrator and services.
- **README.** Re-check every factual claim against the code; fix the health endpoint names and the notifications line.

Done when:

- With two API replicas, a move made through one replica reaches a board connected to the other.
- A test shows a failed `POST` is sent exactly once and a failed `GET` is retried.
- `az deployment group what-if` shows probes, sticky sessions and internal API ingress.
- After a deploy, a request trace spanning web and API is visible in Application Insights.

## Contract changes

REST:

| Today | After | Why |
|---|---|---|
| `PUT /api/tasks/{id}` with optional `title`, `description`, `assigneeId`; a missing key means "leave unchanged" | `PUT /api/tasks/{id}` with `title` and `description`, both always sent; `PATCH /api/tasks/{id}/assignee` with `assigneeId` (nullable) | Removes the custom converter and the absent-versus-null rule. |
| `POST /api/tasks/{taskId}/comments` with `authorId` | same route, body is `text` only | Author comes from the token. |
| `PUT /api/comments/{id}` with `requestingUserId` | same route, body is `text` only | Same. |
| `DELETE /api/comments/{id}?requestingUserId=` | `DELETE /api/comments/{id}` | Same. |
| none | `POST /api/auth/token` | Demo sign-in. |

SignalR: method names and payload fields stay the same on the wire. Statuses are still strings in JSON; only the C# types change.

Update `specs/001-create-taskify/contracts/` or supersede it in the new spec.

## Decisions to confirm in the spec session

| # | Decision | Recommendation | Alternative |
|---|---|---|---|
| D1 | Minimal APIs or controllers | Minimal APIs: validation, typed results and OpenAPI schemas come from the framework. | Keep controllers; return `ActionResult<T>` and add `[ProducesResponseType]`. Smaller diff, less of .NET 10. |
| D2 | Identity model | API-issued JWT after the user picker (Phase 3). | A real identity provider such as Microsoft Entra ID: more secure, much larger scope. |
| D3 | Multi-replica SignalR | Redis backplane. Adds one Azure resource and its cost. | Cap the API at one replica: a one-line Bicep change that is honest about the limit. |
| D4 | API ingress | Internal only. | Keep external and rely on tokens alone. |
| D5 | Task update contract | Split into `PUT` and `PATCH .../assignee`. | Keep one `PUT` and the converter. |
| D6 | Spec Kit slicing | Four specs, one per phase, in order. Each is shippable alone. | One spec with four prioritised user-story groups. |

## Out of scope

- Notifications; the stub endpoint stays.
- Keeping the Active User across a browser refresh.
- Optimistic concurrency on tasks.
- PostgreSQL managed-identity authentication and private networking.
- Moving to xUnit v3 and Microsoft.Testing.Platform.
- Native AOT and trimming.
- Visual or interaction changes to the board.
