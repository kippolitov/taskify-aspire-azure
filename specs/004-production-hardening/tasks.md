---

description: "Task list for 004 Production Hardening"
---

# Tasks: Production Hardening

**Input**: Design documents from `specs/004-production-hardening/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/rest-api.md](contracts/rest-api.md), [contracts/signalr-hub.md](contracts/signalr-hub.md), [quickstart.md](quickstart.md)

**Tests**: Required. Constitution Principle II mandates TDD, and the spec requires it (FR-009, SC-002). In every story, the test tasks come first and **must fail before** the implementation tasks start.

**Organization**: One phase per user story, in priority order (P1 → P4). Story phases depend on each other in order (see Dependencies), because each story is proven with the harness from the stories before it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel: a different file, and no dependency on an unfinished task.
- **[Story]**: US1–US4, mapped to spec stories P1–P4.
- Paths are relative to the repository root. The solution file is `Taskify.slnx`.

## Conventions every task follows

- Package versions go in `Directory.Packages.props` only. A `.csproj` gets a version-less `<PackageReference>`.
- The domain terms come from `CONTEXT.md`: Task, Status, Project, User, Active User, Comment, Author. `TaskItem` is the code name of the entity only.
- Keep cyclomatic complexity at 10 or less per method; S1541 enforces it from T003.
- Run `dotnet csharpier format .` before each commit. CI runs `dotnet format --verify-no-changes`.
- Integration tests use the fixture from Phase 2. Never use an in-memory database.

---

## Phase 1: Setup (shared tooling)

**Purpose**: Tooling that every story uses.

- [X] T001 Add the ReportGenerator local tool (`dotnet-reportgenerator-globaltool`, latest stable 5.x, `"rollForward": false`) to `dotnet-tools.json` beside `csharpier`, then run `dotnet tool restore`.
- [X] T002 [P] In `.editorconfig`, under `[*.{cs,csx,razor}]`, add `dotnet_diagnostic.S1541.severity = warning`. `TreatWarningsAsErrors` in `Directory.Build.props` makes it a build error. Run `dotnet build Taskify.slnx -c Release`. If S1541 reports existing methods over 10, split those methods without changing behaviour, then confirm the build is clean (research R5).
- [X] T003 [P] In `.editorconfig`, under `[*.{cs,csx,razor}]`, add `dotnet_diagnostic.CA2016.severity = warning`, so the build fails when an available `CancellationToken` is not forwarded (research R12). Expect no existing violations, because no code takes a token yet.

---

## Phase 2: Foundational (real-database test harness)

**Purpose**: The integration-test harness that every story's tests run on (research R1).

**⚠️ CRITICAL**: No story's tests can be written until this phase is complete.

- [X] T004 Add `public partial class Program;` as the last line of `src/Taskify.Api/Program.cs`, so `WebApplicationFactory<Program>` can host the API.
- [X] T005 In `tests/Taskify.Api.Tests/Taskify.Api.Tests.csproj`:
  - Remove the `Microsoft.EntityFrameworkCore.InMemory` reference.
  - Keep `Testcontainers.PostgreSQL` and `Microsoft.AspNetCore.Mvc.Testing`.
  - Add `<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />`, if it is not already transitive through the API project.
  - Confirm the project restores.
- [X] T006 Create `tests/Taskify.Api.Tests/Infrastructure/SharedPostgres.cs`, a static class:
  - It holds one `Testcontainers.PostgreSql.PostgreSqlContainer`, built with `new PostgreSqlBuilder("postgres:16").Build()`.
  - Start it through a `Lazy<Task<PostgreSqlContainer>>`, so it starts once per test process. Rely on the Testcontainers reaper for cleanup; do not dispose it in tests.
  - Expose `Task<string> CreateDatabaseAsync(string name)`. It opens an `NpgsqlConnection` to the container's admin connection string, runs `CREATE DATABASE "<name>"`, and returns a connection string with `Database=<name>`.
  - Expose `Task DropDatabaseAsync(string name)`, which uses `DROP DATABASE … WITH (FORCE)`.
- [X] T007 Create `tests/Taskify.Api.Tests/Infrastructure/TaskifyApiFixture.cs`, a class implementing `IAsyncLifetime`, used through `IClassFixture<TaskifyApiFixture>`. In `InitializeAsync`:
  1. Create the database `taskify_<guid N>` through `SharedPostgres`.
  2. Apply the production migrations with `new TaskifyDbContext(new DbContextOptionsBuilder<TaskifyDbContext>().UseNpgsql(cs).UseSnakeCaseNamingConvention().Options).Database.MigrateAsync()`.
  3. Create a `WebApplicationFactory<Program>` with `UseEnvironment("Development")` and `UseSetting("ConnectionStrings:taskifydb", cs)`.

  Expose:
  - `Factory` and `ConnectionString`.
  - `HttpClient CreateClient()`.
  - `TaskifyDbContext CreateDbContext()`, for assertions on database state.
  - `Task<WebApplicationFactory<Program>> CreateFactoryAsync(Action<IWebHostBuilder>? configure)`, for tests that need extra settings.

  `DisposeAsync` disposes the factory and drops the database.
- [X] T008 Create `tests/Taskify.Api.Tests/Infrastructure/HubTestClient.cs`. It wraps a `HubConnection` built with:
  - `.WithUrl(new Uri(server.BaseAddress, "hubs/taskify"), o => { o.HttpMessageHandlerFactory = _ => server.CreateHandler(); o.Transports = HttpTransportType.LongPolling; })`
  - JSON protocol options camelCase, with `JsonStringEnumConverter`.

  Expose:
  - `Task JoinBoardAsync(int projectId)`.
  - `Task<T> WaitForAsync<T>(string methodName, TimeSpan timeout)`, which registers `On<T>` and completes a `TaskCompletionSource<T>` on the first message.
  - `DisposeAsync`.
- [X] T009 Create `tests/Taskify.Api.Tests/Infrastructure/FixtureSmokeTests.cs`, with `[Fact] GetUsers_ReturnsFiveSeededUsers_FromPostgres`: `GET /api/users` returns 200 and five Users. Run it. If the API ignores the `UseSetting` connection string, switch T007 to setting the environment variable `ConnectionStrings__taskifydb` before the factory starts, and record that in `research.md` R1 (plan risk "UseSetting not reaching Aspire").

**Checkpoint**: The harness runs against real PostgreSQL 16. Story phases can start.

---

## Phase 3: User Story 1 – Trustworthy verification (Priority: P1) 🎯 MVP

**Goal**: A green pipeline means the release works: tests go through the full pipeline to real PostgreSQL, one component owns the schema, images build, and coverage is gated (FR-001–FR-010).

**Independent Test**: [quickstart.md § P1](quickstart.md#p1--trustworthy-verification). The suite passes on a clean checkout, and each of the four deliberate breaks turns the pipeline red (SC-002).

### Tests for User Story 1 (write first, confirm they fail where noted) ⚠️

- [X] T010 [P] [US1] Create `tests/Taskify.Api.Tests/Endpoints/UserEndpointsTests.cs` and `ProjectEndpointsTests.cs`. Port every case from `tests/Taskify.Api.Tests/Controllers/IntegrationControllerTests.cs` (the `UsersControllerTests` and `ProjectsControllerTests` classes) to HTTP calls through `TaskifyApiFixture.CreateClient()`. Assert status codes, the `application/problem+json` content type on 404, and bodies, against the seeded data (Users 1–5, Projects 1–3 from `TaskifyDbContext.OnModelCreating`).
- [X] T011 [P] [US1] Create `tests/Taskify.Api.Tests/Endpoints/TaskEndpointsTests.cs`. Port every case from `tests/Taskify.Api.Tests/Controllers/TasksControllerTests.cs` to HTTP calls:
  - `GET /api/projects/{id}/tasks`, and `GET /api/tasks/{id}`.
  - `POST` create returns 201 with `Location` `/api/tasks/{id}`.
  - `PUT` update, and `PATCH` status.
  - 404 and 422 cases.
  - Assert database state through `fixture.CreateDbContext()`.
- [X] T012 [P] [US1] Create `tests/Taskify.Api.Tests/Endpoints/CommentEndpointsTests.cs` and `NotificationEndpointsTests.cs`. Port every case from `tests/Taskify.Api.Tests/Controllers/CommentsControllerTests.cs`, and the notifications case from `IntegrationControllerTests.cs`, to HTTP calls. Use today's contract: `authorId` and `requestingUserId` are still in the request.
- [X] T013 [P] [US1] Create `tests/Taskify.Api.Tests/Endpoints/ValidationTests.cs` (FR-004):
  - `PATCH /api/tasks/1/status` with `{ "status": "Archived" }` returns 400 with content type `application/problem+json`.
  - Then a fresh `TaskifyDbContext` shows Task 1's `Status` and `UpdatedAt` unchanged.
- [X] T014 [P] [US1] Create `tests/Taskify.Api.Tests/Realtime/BroadcastTests.cs` (FR-005):
  - A `HubTestClient` joins board 1, the test sends `PATCH /api/tasks/1/status` `{ "status": "Done" }`, and the client receives `TaskMoved` within 2 s with `taskId` 1 and `toStatus` `Done`.
  - Port the four group cases from `tests/Taskify.Api.Tests/Hubs/TaskifyHubTests.cs` as observable behaviour: after `LeaveBoard`, no message arrives within 1 s.
- [X] T015 [P] [US1] Create `tests/Taskify.Api.Tests/Data/SchemaOwnershipTests.cs` (FR-006):
  - Create an empty database with `SharedPostgres.CreateDatabaseAsync`, without migrating it.
  - Start a `WebApplicationFactory<Program>` against it, and call `CreateClient()` to force startup.
  - Assert, through `NpgsqlConnection`, that table `__EFMigrationsHistory` does **not** exist.
  - **This must fail now**, because the API migrates at startup.

### Implementation for User Story 1

- [X] T016 [US1] Delete the migration block (the `using (var scope …) { … MigrateAsync … }` lines and the `// Apply EF Core migrations` comment) from `src/Taskify.Api/Program.cs`. T015 now passes. `src/Taskify.AppHost/Program.cs` already orders `taskify-migrate` with `WaitForCompletion`, so make no change there.
- [X] T017 [US1] Delete `tests/Taskify.Api.Tests/Controllers/`, `tests/Taskify.Api.Tests/Helpers/TestHelpers.cs` and `tests/Taskify.Api.Tests/Hubs/TaskifyHubTests.cs`. Keep `tests/Taskify.Api.Tests/Services/TaskServiceToDtoTests.cs`, which is pure mapping with no database. Run `dotnet test tests/Taskify.Api.Tests`: everything passes on PostgreSQL.
- [X] T018 [P] [US1] Point the benchmarks in `tests/Taskify.Benchmarks/` at PostgreSQL:
  - In `tests/Taskify.Benchmarks/Taskify.Benchmarks.csproj`, replace `Microsoft.EntityFrameworkCore.InMemory` with `Testcontainers.PostgreSQL`.
  - In `tests/Taskify.Benchmarks/ApiBenchmarks.cs`, `[GlobalSetup]` starts `new PostgreSqlBuilder("postgres:16").Build()`, migrates with `MigrateAsync`, and seeds 20 Tasks. `[GlobalCleanup]` disposes the container.
  - Replace `BenchmarkNullHubContext` with the real `IHubContext` from a minimal `ServiceCollection` (`AddSignalR()`), or keep the no-op, but keep it `file`-scoped.
- [X] T019 [US1] Remove the `Microsoft.EntityFrameworkCore.InMemory` `PackageVersion` from `Directory.Packages.props`. Verify with `grep -rn "InMemory" tests/ src/ --include=*.csproj`: it must return nothing (FR-002).
- [X] T020 [P] [US1] In `src/Taskify.Api/Dockerfile` and `src/Taskify.Web/Dockerfile`, add `COPY Directory.Packages.props .` directly after `COPY Directory.Build.props .`. Verify that `docker build -f src/Taskify.Api/Dockerfile .` and `docker build -f src/Taskify.Web/Dockerfile .` both succeed. Before the fix, restore failed with NU1015 (research R3).
- [X] T021 [US1] In `.github/workflows/ci.yml`, add a job `docker-build` (`runs-on: ubuntu-latest`, `needs: build`):
  - Checkout, then `docker build -f src/Taskify.Api/Dockerfile -t taskify-api:ci .` and `docker build -f src/Taskify.Web/Dockerfile -t taskify-web:ci .`.
  - No push, so FR-007 is a pure build check.
  - Runs on `pull_request` and on `push` to `main`, inherited from the workflow triggers.
- [X] T022 [US1] In `.github/workflows/ci.yml`, job `test` (research R4):
  1. Add `--settings coverage.runsettings` to both `dotnet test` commands.
  2. Add a step `dotnet tool restore`.
  3. Add a step `dotnet reportgenerator -reports:"test-results/**/coverage.opencover.xml" -targetdir:coverage -reporttypes:"JsonSummary;MarkdownSummaryGithub"`.
  4. Add a step that reads `coverage/Summary.json` with `jq -r '.summary.linecoverage'`, prints `Line coverage: <n>%`, appends `coverage/SummaryGithub.md` to `$GITHUB_STEP_SUMMARY`, and `exit 1` when the value is below 80 (FR-008).

  Keep the Codecov upload as is.
- [X] T023 [US1] In `.github/workflows/ci.yml`, job `test`, add the constitution's critical-path gate after the 80% gate (constitution Principle II: "Critical paths (auth, data persistence, task mutation flows): ≥ 95% coverage"; Technical Quality Gate 2):
  1. Run ReportGenerator a second time on the same reports: `dotnet reportgenerator -reports:"test-results/**/coverage.opencover.xml" -targetdir:coverage-critical -reporttypes:"JsonSummary;MarkdownSummaryGithub" -assemblyfilters:"+Taskify.Api" -classfilters:"+Taskify.Api.Identity.*;+Taskify.Api.Services.*;+Taskify.Api.Endpoints.*"`. A filter that matches nothing yet, such as `Identity` before US3, is fine.
  2. Read `coverage-critical/Summary.json` with `jq -r '.summary.linecoverage'`, print `Critical-path line coverage: <n>%`, append `coverage-critical/SummaryGithub.md` to `$GITHUB_STEP_SUMMARY`, and `exit 1` when the value is below 95.
  3. If US1's tests leave `src/Taskify.Api/Services/` below 95%, add HTTP tests in `tests/Taskify.Api.Tests/Endpoints/` until the gate passes; do not lower the threshold.
- [X] T024 [US1] Update `coverage.runsettings`. Replace the comment block that claims thresholds "are enforced by the coverlet.msbuild property" with one line: the 80% overall and 95% critical-path line-coverage gates are enforced in `.github/workflows/ci.yml` through ReportGenerator. Keep `<Format>opencover</Format>`.
- [X] T025 [US1] Update `README.md` (FR-010):
  - In "DevOps & CI/CD", change pipeline step 1 to describe the actual suite: API tests run against PostgreSQL 16 in Docker through Testcontainers and the full HTTP pipeline; bUnit tests cover the Blazor components; the pipeline enforces an 80% overall and a 95% critical-path line-coverage gate and builds both container images.
  - In "Run tests", add that Docker must be running.
- [X] T026 [US1] Validate US1: run every step of [quickstart.md § P1](quickstart.md#p1--trustworthy-verification), including the four deliberate breaks on a throwaway branch (SC-002). Record the outcome in the PR description.

**Checkpoint**: US1 is complete. The tests are trustworthy; this is the MVP.

---

## Phase 4: User Story 2 – Consistent, self-describing API (Priority: P2)

**Goal**: One validation mechanism, one error format, a complete API description, cancellation, and shared request, response and real-time definitions (FR-011–FR-020). Comment requests still carry `authorId` and `requestingUserId`; US3 removes them.

**Independent Test**: [quickstart.md § P2](quickstart.md#p2--consistent-self-describing-api).

### Setup for User Story 2

- [X] T027 [US2] Add the US2 packages to `Directory.Packages.props` and the project files:
  - `PackageVersion`s for `Microsoft.Extensions.Validation` and `Microsoft.Extensions.TimeProvider.Testing` (latest stable 10.x) in `Directory.Packages.props`.
  - References to `Microsoft.Extensions.Validation` in `src/Taskify.Shared/Taskify.Shared.csproj`.
  - References to `Microsoft.Extensions.TimeProvider.Testing` in `tests/Taskify.Api.Tests/Taskify.Api.Tests.csproj` and `tests/Taskify.Web.Tests/Taskify.Web.Tests.csproj`.

### Tests for User Story 2 (write first, confirm they fail) ⚠️

- [X] T028 [P] [US2] Create `tests/Taskify.Api.Tests/Contract/OpenApiContractTests.cs` with `[Trait("Category","Contract")]` (FR-013, SC-003):
  - Fetch `/openapi/v1.json` through the fixture, which runs in Development, and parse it with `JsonDocument`.
  - Assert that `openapi` starts with `3.1`.
  - Assert that every operation under `paths` has at least one `2xx` and at least one `4xx` or `5xx` response.
  - Assert that `PUT /api/tasks/{id}` and `PATCH /api/tasks/{id}/assignee` both exist.
- [X] T029 [P] [US2] Create `tests/Taskify.Api.Tests/Contract/HubContractTests.cs` with `[Trait("Category","Contract")]` (FR-018):
  - For each of the six `ITaskifyClient` methods, trigger the matching REST operation. Receive through `HubTestClient.WaitForAsync<TMessage>(nameof(ITaskifyClient.X), …)`, typed with the shared message record from `Taskify.Shared.Realtime`.
  - Assert the fields match [contracts/signalr-hub.md](contracts/signalr-hub.md). `FromStatus` and `ToStatus` arrive as `ColumnStatus`.
- [X] T030 [P] [US2] Extend `tests/Taskify.Api.Tests/Endpoints/ValidationTests.cs` (FR-011):
  - For every operation that takes input, send input that breaks one rule. Assert 400, `application/problem+json`, and an `errors` object keyed by the failing field:
    - `POST /api/projects/1/tasks`, and `PUT /api/tasks/1`, with `title` `""` and with 301 characters.
    - `PATCH /api/tasks/1/status` with `{ "status": 7 }`.
    - `POST /api/tasks/1/comments`, and `PUT /api/comments/1`, with `text` `""` and with 10,001 characters.
  - Assert that every validation body has the same top-level keys.
- [X] T031 [P] [US2] Create `tests/Taskify.Api.Tests/Endpoints/ErrorFormatTests.cs` (FR-012):
  - **Not-found shape**: `GET` `/api/tasks/99999`, `/api/projects/99999`, `/api/users/99999` and `/api/tasks/99999/comments` all return 404 with identical top-level keys (`type`, `title`, `status`, `detail`).
  - **500 with no internals**: use `CreateFactoryAsync` with `UseSetting("ConnectionStrings:taskifydb", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1")`. `GET /api/projects` returns 500 `application/problem+json`, and the body has no `exception`, `stackTrace` or `detail` containing "Npgsql".
- [X] T032 [P] [US2] Extend `tests/Taskify.Api.Tests/Endpoints/TaskEndpointsTests.cs` (FR-015):
  - `PUT /api/tasks/2` with `{ "title": "New", "description": null }` sets the title, clears the description, and leaves `AssigneeId` (2) unchanged.
  - `PATCH /api/tasks/2/assignee` with `{ "assigneeId": 3 }` sets the Assignee and leaves the title unchanged. A `HubTestClient` receives `TaskAssigned`.
  - `{ "assigneeId": null }` clears the Assignee.
  - `{ "assigneeId": 999 }` returns 422.
  - Repeating the same `assigneeId` sends no `TaskAssigned` within 1 s.
- [X] T033 [P] [US2] Create `tests/Taskify.Api.Tests/Data/TaskListQueryTests.cs` (FR-014):
  - Build a `TaskifyDbContext` with `UseNpgsql(fixture.ConnectionString).UseSnakeCaseNamingConvention()`.
  - Call `TaskService.ProjectTasksQuery(db, 1)`: the `internal static IQueryable<TaskDto>` builder that T043 creates, visible to tests through T051.
  - Assert that `ToQueryString()` contains `count(` and does not contain `c.text`.
  - Also assert that the API result's `commentCount` for Task 1 equals the number of rows in `comments`.
- [X] T034 [P] [US2] Create `tests/Taskify.Api.Tests/Data/CancellationTests.cs` (FR-016):
  - Resolve `TaskService` from `fixture.Factory.Services` in a scope, and call `MoveTaskAsync(1, ColumnStatus.Done, cancelledToken)`.
  - Assert that it throws `OperationCanceledException`, that Task 1's Status is unchanged in the database, and that a joined `HubTestClient` receives no `TaskMoved` within 1 s.
- [X] T035 [P] [US2] Update `tests/Taskify.Web.Tests/Components/TaskDetailTests.cs` and `tests/Taskify.Web.Tests/Helpers/TestApiHandler.cs` for the split update:
  - Saving a title or description sends `PUT /api/tasks/{id}` with only `title` and `description`.
  - Changing the Assignee sends `PATCH /api/tasks/{id}/assignee`.
  - Record the method and path of each request in `TestApiHandler` so tests can assert them.

### Implementation for User Story 2

- [X] T036 [P] [US2] Create `src/Taskify.Shared/TaskifyJson.cs`:
  - `public static class TaskifyJson` with `public static void Configure(JsonSerializerOptions o)`. It sets `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`, `PropertyNameCaseInsensitive = true`, and adds `new JsonStringEnumConverter()`.
  - `public static JsonSerializerOptions Options { get; }`, created from `JsonSerializerDefaults.Web` and passed through `Configure` (FR-020).
- [X] T037 [P] [US2] Create the request records in `src/Taskify.Shared/Requests/`, one file each, using DataAnnotations. Apply the rules verbatim from [data-model.md](data-model.md):
  - `CreateTaskRequest(string Title, string? Description, int? AssigneeId)`: "`Title` required, 1–300 characters. `AssigneeId`, when present, must name an existing User; otherwise 422, checked in the service." Use `[Required][StringLength(300, MinimumLength = 1)]` on `Title`.
  - `UpdateTaskRequest(string Title, string? Description)`: "`Title` required, 1–300 characters. `Description` replaces the current value, so `null` clears it."
  - `AssignTaskRequest(int? AssigneeId)`: "`null` clears the Assignee. A value must name an existing User; otherwise 422."
  - `MoveTaskRequest(ColumnStatus Status)`: "Required, and must be one of the four Statuses. An unknown name fails JSON binding with 400. An undefined number, such as `7`, which the string-enum converter would otherwise accept, is rejected by `[EnumDataType(typeof(ColumnStatus))]` with 400." Use `[Required][property: JsonRequired][EnumDataType(typeof(ColumnStatus))]`.
  - `AddCommentRequest([Required][property: JsonRequired] int AuthorId, string Text)` and `EditCommentRequest([Required][property: JsonRequired] int RequestingUserId, string Text)`. `Text`: "Required, 1–10,000 characters", so use `[Required][StringLength(10000, MinimumLength = 1)]`. The id fields are removed in US3.
- [X] T038 [P] [US2] Create `src/Taskify.Shared/Realtime/ITaskifyClient.cs` with methods:
  - `Task TaskMoved(TaskMovedMessage message)`
  - `Task TaskAssigned(TaskAssignedMessage message)`
  - `Task TaskCreated(TaskDto task)`
  - `Task CommentAdded(CommentAddedMessage message)`
  - `Task CommentEdited(CommentEditedMessage message)`
  - `Task CommentDeleted(CommentDeletedMessage message)`

  Create one positional record per message in the same folder, with the fields from [data-model.md](data-model.md):
  - `TaskMovedMessage(int TaskId, int ProjectId, ColumnStatus FromStatus, ColumnStatus ToStatus, DateTimeOffset MovedAt)`
  - `TaskAssignedMessage(int TaskId, int ProjectId, UserDto? Assignee)`
  - `CommentAddedMessage(int TaskId, CommentDto Comment)`
  - `CommentEditedMessage(int TaskId, int CommentId, string NewText, DateTimeOffset? EditedAt)`
  - `CommentDeletedMessage(int TaskId, int CommentId)`
- [X] T039 [P] [US2] Create `src/Taskify.Shared/ValidationRegistration.cs` with `public static IServiceCollection AddTaskifyContractValidation(this IServiceCollection services) => services.AddValidation();`. This makes the validation source generator cover the `Taskify.Shared` request records (research R6).
- [X] T040 [P] [US2] Create `src/Taskify.Api/Results/Result.cs`:
  - `public enum ErrorKind { NotFound, Forbidden, Unprocessable }`.
  - `public sealed record Error(ErrorKind Kind, string Detail)`.
  - `public readonly record struct Result<T>`, with `T? Value`, `Error? Error`, `bool IsSuccess`, and implicit conversions from `T` and from `Error`.
  - A non-generic `Result` with `Success` and an implicit conversion from `Error`, for deletes.
- [X] T041 [US2] Create `src/Taskify.Api/Results/ResultHttpExtensions.cs` (depends on T040). This is the only place outcomes become HTTP:
  - `ToHttpResult<T>(this Result<T>)` returns `Results<Ok<T>, ProblemHttpResult>`.
  - `ToCreatedResult<T>(this Result<T>, Func<T, string> location)` returns `Results<Created<T>, ProblemHttpResult>`.
  - `ToNoContentResult(this Result)` returns `Results<NoContent, ProblemHttpResult>`.
  - Errors map `NotFound` to 404, `Forbidden` to 403 and `Unprocessable` to 422, through `TypedResults.Problem(detail: e.Detail, statusCode: …)`.
- [X] T042 [P] [US2] Create `src/Taskify.Api/ErrorHandling/UnexpectedExceptionHandler.cs`, an `IExceptionHandler`. It logs the exception at Error level and writes through `IProblemDetailsService` a 500 `ProblemDetails` with `Title = "An unexpected error occurred."` and no detail or exception data (FR-012).
- [X] T043 [US2] Rewrite `src/Taskify.Api/Services/TaskService.cs` (depends on T036–T041):
  - **Constructor**: `TaskService(TaskifyDbContext db, IHubContext<TaskifyHub, ITaskifyClient> hub, TimeProvider time)`.
  - **Projection**: a private static `Expression<Func<TaskItem, TaskDto>> ToDto` that maps `CommentCount = t.Comments.Count` and `Assignee` to `UserDto` or `null`. Use it in `ProjectTasksQuery` (internal static, see T033) and `GetTaskAsync`, with `AsNoTracking()` and no `Include`.
  - **Methods**, each taking a `CancellationToken` passed to every EF call:
    - `Task<Result<IReadOnlyList<TaskDto>>> GetProjectTasksAsync(int projectId, ct)`: NotFound when the Project is missing.
    - `Task<Result<TaskDto>> GetTaskAsync(int id, ct)`.
    - `CreateTaskAsync(int projectId, CreateTaskRequest r, ct)`: NotFound for a missing Project, Unprocessable for a missing Assignee.
    - `UpdateTaskAsync(int id, UpdateTaskRequest r, ct)`: sets `Title` and `Description` only.
    - `AssignTaskAsync(int id, AssignTaskRequest r, ct)`: broadcasts `TaskAssigned` only when the Assignee changed.
    - `MoveTaskAsync(int id, ColumnStatus status, ct)`.
  - **Time**: use `time.GetUtcNow()` instead of `DateTimeOffset.UtcNow`.
  - **Broadcasts**: through `hub.Clients.Group($"board-{projectId}").TaskMoved(new TaskMovedMessage(…))` and so on, after `SaveChangesAsync(ct)`.
  - **Removed**: the tuples, `null!`, and the old `ToDto(TaskItem)` method. Update `TaskServiceToDtoTests.cs` to test the expression with `ToDto.Compile()`.
- [X] T044 [US2] Rewrite `src/Taskify.Api/Services/CommentService.cs` in the same style:
  - Constructor takes `IHubContext<TaskifyHub, ITaskifyClient>` and `TimeProvider`.
  - Methods return `Result<IReadOnlyList<CommentDto>>`, `Result<CommentDto>` or `Result`, and take `CancellationToken`.
  - Moved from the controller: the Author-exists check (Unprocessable) and the Task-exists check (NotFound).
  - Forbidden when the requester is not the Author.
  - Broadcasts `CommentAdded`, `CommentEdited` and `CommentDeleted` with the shared message records.
  - Keep the `authorId` and `requestingUserId` parameters for now.
- [X] T045 [US2] Change `src/Taskify.Api/Hubs/TaskifyHub.cs` to `public class TaskifyHub : Hub<ITaskifyClient>`. `JoinBoard` and `LeaveBoard` are unchanged.
- [X] T046 [P] [US2] Create `src/Taskify.Api/Endpoints/UserEndpoints.cs`, `ProjectEndpoints.cs` and `NotificationEndpoints.cs`:
  - Each is a static class with `public static RouteGroupBuilder MapXxxEndpoints(this IEndpointRouteBuilder routes)`, using `MapGroup("/api/users")` and so on with `.WithTags(...)`.
  - Handlers are private static methods taking `TaskifyDbContext db` and `CancellationToken ct`. They return `Results<Ok<T>, ProblemHttpResult>`, or `Ok<T>` for lists, and project with `AsNoTracking()`.
  - Declare `.ProducesProblem(StatusCodes.Status404NotFound)` where applicable, and `.ProducesProblem(StatusCodes.Status500InternalServerError)` on each group.
  - Notifications return `TypedResults.Ok(Array.Empty<NotificationDto>())`.
- [X] T047 [US2] Create `src/Taskify.Api/Endpoints/TaskEndpoints.cs` (depends on T043), with routes from [contracts/rest-api.md § Tasks](contracts/rest-api.md#tasks):
  - `GET /api/projects/{projectId:int}/tasks` and `GET /api/tasks/{id:int}`.
  - `POST /api/projects/{projectId:int}/tasks`, returning `ToCreatedResult(t => $"/api/tasks/{t.Id}")`.
  - `PUT /api/tasks/{id:int}`, `PATCH /api/tasks/{id:int}/assignee` and `PATCH /api/tasks/{id:int}/status`.

  Each handler calls `TaskService` and `ToHttpResult()`. Declare `.ProducesValidationProblem()` on operations with a body, plus `.ProducesProblem(404)` and `.ProducesProblem(422)` as the contract lists.
- [X] T048 [US2] Create `src/Taskify.Api/Endpoints/CommentEndpoints.cs` (depends on T044), with routes from [contracts/rest-api.md § Comments](contracts/rest-api.md#comments), keeping the 1.0.0 `authorId`, `requestingUserId` body fields and the `?requestingUserId=` query:
  - `GET /api/tasks/{taskId:int}/comments`.
  - `POST /api/tasks/{taskId:int}/comments`, returning 201 with `Location` `/api/tasks/{taskId}/comments/{id}`.
  - `PUT /api/comments/{id:int}`, and `DELETE /api/comments/{id:int}` returning 204.
- [X] T049 [US2] Rewrite the composition in `src/Taskify.Api/Program.cs`:
  - **Remove**: `AddControllers().AddJsonOptions(...)` and `MapControllers()`.
  - **Add**: `ConfigureHttpJsonOptions(o => TaskifyJson.Configure(o.SerializerOptions))`, `AddValidation()`, `AddTaskifyContractValidation()`, `AddProblemDetails()`, `AddExceptionHandler<UnexpectedExceptionHandler>()` and `AddSingleton(TimeProvider.System)`.
  - **SignalR**: `AddSignalR().AddJsonProtocol(o => TaskifyJson.Configure(o.PayloadSerializerOptions))`.
  - **Pipeline**: `app.UseExceptionHandler()` and `app.UseStatusCodePages()` before mapping, then `MapUserEndpoints()`, `MapProjectEndpoints()`, `MapTaskEndpoints()`, `MapCommentEndpoints()`, `MapNotificationEndpoints()` and `MapHub<TaskifyHub>("/hubs/taskify")`.
- [X] T050 [US2] Delete `src/Taskify.Api/Controllers/`, which includes `UpdateTaskRequestConverter` and the old request records. Run `dotnet test tests/Taskify.Api.Tests`: T010–T015 and T028–T034 pass.
- [X] T051 [P] [US2] Create `src/Taskify.Api/Properties/AssemblyInfo.cs` with `[assembly: InternalsVisibleTo("Taskify.Api.Tests")]`.
- [X] T052 [US2] Rewrite `src/Taskify.Web/Services/ApiClient.cs`:
  - Use `TaskifyJson.Options` and the shared request records instead of anonymous objects.
  - Add a `CancellationToken ct = default` parameter to every method.
  - Replace `UpdateTaskAsync(id, title, description, assigneeId)` with `UpdateTaskAsync(int id, string title, string? description, ct)`, which sends `PUT` with an `UpdateTaskRequest`.
  - Replace `UpdateTaskAssigneeAsync` with `AssignTaskAsync(int id, int? assigneeId, ct)`, which sends `PATCH /api/tasks/{id}/assignee`.
- [X] T053 [US2] Update `src/Taskify.Web/Components/Shared/TaskDetail.razor`: save edit calls `Api.UpdateTaskAsync(Task.Id, _editTitle, _editDescription)`, and the assignee change calls `Api.AssignTaskAsync(Task.Id, assigneeId)`. Behaviour and markup are unchanged.
- [X] T054 [US2] Rewrite the handlers in `src/Taskify.Web/Services/BoardHubClient.cs`:
  - Register with `conn.On<TaskMovedMessage>(nameof(ITaskifyClient.TaskMoved), …)` and so on for all six, using the shared records.
  - Delete the private payload records.
  - Change the events to `Func<TaskMovedMessage, Task>`, `Func<TaskAssignedMessage, Task>`, `Func<TaskDto, Task>`, `Func<CommentAddedMessage, Task>`, `Func<CommentEditedMessage, Task>` and `Func<CommentDeletedMessage, Task>`.
  - Configure the hub JSON protocol with `TaskifyJson.Configure`.
- [X] T055 [US2] Update `src/Taskify.Web/Components/Pages/KanbanBoard.razor`:
  - Change the SignalR handlers to the new event signatures. `HandleTaskMovedSignalR(TaskMovedMessage m)` uses `m.ToStatus` directly, with no `Enum.TryParse`.
  - Add `@key="task.Id"` to the `<TaskCard>` in the column loop (research R13).
- [X] T056 [P] [US2] Add `[PersistentState] public List<UserDto>? Users { get; set; }` to `src/Taskify.Web/Components/Pages/Home.razor`, loaded with `Users ??= await Api.GetUsersAsync() ?? [];`. Add `[PersistentState] public List<ProjectDto>? Projects { get; set; }` to `src/Taskify.Web/Components/Pages/ProjectList.razor` the same way. Replace the private `_users` and `_projects` fields. The rendered markup is unchanged.
- [X] T057 [P] [US2] In `src/Taskify.Web/Services/IdentityService.cs`, replace `_currentUser` with a C# 14 field-backed property: `public UserDto? CurrentUser { get; private set { field = value; OnChange?.Invoke(); } }`. `SetUser` and `ClearUser` assign the property.
- [X] T058 [US2] Update `tests/Taskify.Web.Tests/` (`Helpers/TestApiHandler.cs`, `Components/KanbanBoardTests.cs`, `KanbanBoardDragTests.cs`, `SortableInteropJsTests.cs`, `TaskDetailTests.cs`) for the new `ApiClient` and `BoardHubClient` signatures and `TaskifyJson.Options`. Run `dotnet test tests/Taskify.Web.Tests`.
- [X] T059 [P] [US2] Create `tests/Taskify.Benchmarks/TaskListBenchmark.cs` (SC-004):
  - `[GlobalSetup]` seeds one Project with 500 Tasks × 20 Comments in a PostgreSQL 16 container.
  - `[Benchmark]` calls `TaskService.GetProjectTasksAsync`.
  - Use `[Config(typeof(P95Config))]`, which adds `StatisticColumn.P95`.
  - In `tests/Taskify.Benchmarks/Program.cs`, use `BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args)` so `--filter` works.
- [X] T060 [P] [US2] Write `docs/adr/0001-minimal-apis.md` in the format of `.agents/skills/domain-modeling/ADR-FORMAT.md`. One paragraph: Minimal APIs with `AddValidation`, `TypedResults` and one `Result<T>` mapping replace controllers, so that validation, error format and OpenAPI shapes come from the framework (D1). Note the `Taskify.Shared` validation registration (research R6) under **Consequences**.
- [X] T061 [US2] Validate US2: run [quickstart.md § P2](quickstart.md#p2--consistent-self-describing-api), including the SC-004 benchmark (p95 ≤ 200 ms) and the manual two-window board check (FR-041).

**Checkpoint**: US1 and US2 work. The API is consistent and self-describing.

---

## Phase 5: User Story 3 – Server-vouched identity (Priority: P3)

**Goal**: The server issues and checks the Active User's identity. No request names its own caller (FR-021–FR-031).

**Independent Test**: [quickstart.md § P3](quickstart.md#p3--server-vouched-identity).

### Setup for User Story 3

- [X] T062 [US3] Add `PackageVersion` `Microsoft.AspNetCore.Authentication.JwtBearer` (10.0.x, matching the other ASP.NET Core packages) to `Directory.Packages.props`, and a reference in `src/Taskify.Api/Taskify.Api.csproj`.
- [X] T063 [US3] Extend `tests/Taskify.Api.Tests/Infrastructure/TaskifyApiFixture.cs`:
  - `UseSetting("Auth:SigningKey", <fixed 32-byte base64 test key>)`, `UseSetting("Auth:Issuer", "taskify-tests")` and `UseSetting("Auth:Audience", "taskify-api")`.
  - Replace `TimeProvider` with a `FakeTimeProvider` exposed as `fixture.Time`.
  - Add `Task<HttpClient> CreateClientAsAsync(int userId)`, which calls `POST /api/auth/token` and sets the bearer header.
  - Update `HubTestClient` to accept an optional token through `AccessTokenProvider`.

### Tests for User Story 3 (write first, confirm they fail) ⚠️

- [X] T064 [P] [US3] Create `tests/Taskify.Api.Tests/Identity/TokenEndpointTests.cs` (FR-021, FR-022):
  - `POST /api/auth/token` `{ "userId": 2 }` returns 200. `expiresAt` equals `fixture.Time.GetUtcNow() + 15 min`. The JWT's `sub` is `"2"`. Changing one character of the signature makes `GET /api/projects` return 401.
  - `{ "userId": 999 }` returns 404 with no `accessToken` in the body.
  - `{}` returns 400.
  - Advancing `fixture.Time` by 16 minutes makes the token return 401.
- [X] T065 [P] [US3] Create `tests/Taskify.Api.Tests/Identity/AuthorizationTests.cs` (FR-023–FR-027):
  - With no token, every operation in [contracts/rest-api.md](contracts/rest-api.md) except `POST /api/auth/token` and `GET /api/users` returns 401, and changes nothing in the database. Use a theory over method and path.
  - `GET /api/users` with no token returns 200.
  - `POST /api/tasks/1/comments` as User 2 with `{ "text": "hi", "authorId": 1 }` returns 201 with `author.id == 2`.
  - `PUT` and `DELETE` `/api/comments/{id}` as User 3, on User 2's Comment, return 403, and the Comment is unchanged.
  - As User 2, they return 200 and 204.
- [X] T066 [P] [US3] Create `tests/Taskify.Api.Tests/Identity/HubAuthorizationTests.cs` (FR-028): starting a `HubTestClient` with no token fails with an `HttpRequestException` whose status is 401. With a valid token, it connects and receives `TaskMoved`.
- [X] T067 [P] [US3] Create `tests/Taskify.Web.Tests/Services/IdentityServiceRenewalTests.cs` (FR-029). Use a fake `IHttpClientFactory` returning a client whose handler counts calls to `/api/auth/token`, plus `FakeTimeProvider`:
  - `SignInAsync(user)` obtains a token.
  - `GetAccessTokenAsync()` returns the cached token while more than 2 minutes remain.
  - It renews exactly once when less than 2 minutes remain, even with 10 concurrent callers.
- [X] T068 [P] [US3] Create `tests/Taskify.Web.Tests/Services/ApiClientAuthTests.cs`: every `ApiClient` call carries `Authorization: Bearer <token from IdentityService>`. Assert it on the requests recorded by `TestApiHandler`.

### Implementation for User Story 3

- [X] T069 [P] [US3] Create the identity contract types in `src/Taskify.Shared/`:
  - `src/Taskify.Shared/Requests/TokenRequest.cs`: `TokenRequest(int UserId)`, with the rule "Required, and must name an existing User; otherwise 404". Use `[Required][property: JsonRequired]`.
  - `src/Taskify.Shared/Dtos/TokenResponse.cs`: `TokenResponse(string AccessToken, DateTimeOffset ExpiresAt)`.
- [X] T070 [P] [US3] Create `src/Taskify.Api/Identity/AuthOptions.cs` (`SigningKey`, `Issuer`, `Audience`, and `Lifetime` defaulting to 15 minutes), bound from section `Auth`, with `ValidateOnStart` requiring a decoded key of at least 32 bytes.

  Create `src/Taskify.Api/Identity/ClaimsPrincipalExtensions.cs`:

  ```csharp
  public static class ClaimsPrincipalExtensions
  {
      extension(ClaimsPrincipal user)
      {
          public int UserId => int.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub)!, CultureInfo.InvariantCulture);
      }
  }
  ```

  This is a C# 14 extension property (research R14).
- [X] T071 [US3] Create `src/Taskify.Api/Identity/TokenIssuer.cs`. It uses `JsonWebTokenHandler.CreateToken(new SecurityTokenDescriptor { Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId)]), Issuer, Audience, Expires = time.GetUtcNow().Add(Lifetime).UtcDateTime, SigningCredentials = HMAC-SHA256 over the key })` and returns a `TokenResponse`.

  Create `src/Taskify.Api/Identity/AuthEndpoints.cs` with `MapAuthEndpoints()`: `POST /api/auth/token`, `.AllowAnonymous()`, returning 404 Problem Details when the User does not exist, and `.ProducesValidationProblem()` and `.ProducesProblem(404)`.
- [X] T072 [US3] Wire authentication in `src/Taskify.Api/Program.cs`:
  - `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(...)`, with `MapInboundClaims = false` and `TokenValidationParameters` from `AuthOptions`: issuer, audience, signing key and lifetime validated.
  - `AddAuthorization()`, then `app.UseAuthentication()` and `app.UseAuthorization()`.
  - `MapAuthEndpoints()`.
  - Add `.RequireAuthorization()` on the project, task, comment and notification groups, and on `GET /api/users/{id}`. Mark `GET /api/users` `.AllowAnonymous()` (FR-025).
  - Add `.ProducesProblem(401)` on protected groups.
- [X] T073 [US3] Add `[Authorize]` to `src/Taskify.Api/Hubs/TaskifyHub.cs`. Leave `CloseOnAuthenticationExpiration` off, the default, as the spec's edge case requires.
- [X] T074 [US3] Remove caller ids from the comment contract in `src/Taskify.Shared/Requests/`, `src/Taskify.Api/Endpoints/CommentEndpoints.cs` and `src/Taskify.Api/Services/CommentService.cs`:
  - In `src/Taskify.Shared/Requests/AddCommentRequest.cs`, delete `AuthorId`, leaving `AddCommentRequest(string Text)`.
  - In `EditCommentRequest.cs`, delete `RequestingUserId`.
  - In `src/Taskify.Api/Endpoints/CommentEndpoints.cs`, handlers take `ClaimsPrincipal user` and pass `user.UserId` to `CommentService`, and the `DELETE` handler drops `[FromQuery] requestingUserId`.
  - In `src/Taskify.Api/Services/CommentService.cs`, rename parameters to `callerId`, and drop the Author-exists 422 check, because a valid token always names a User (FR-023, FR-026, FR-027).
- [X] T075 [US3] Rewrite `src/Taskify.Web/Services/IdentityService.cs`:
  - Constructor `(IHttpClientFactory httpClientFactory, TimeProvider time)`.
  - `Task SignInAsync(UserDto user, CancellationToken ct)` calls `POST /api/auth/token` through the named client `"auth"` and stores the token and `ExpiresAt`.
  - `Task<string?> GetAccessTokenAsync()` renews through the same client when less than 2 minutes remain, guarded by a `SemaphoreSlim(1,1)`.
  - `ClearUser()` also clears the token.
  - Keep `CurrentUser`, `IsAuthenticated` and `OnChange`.
- [X] T076 [US3] Update `src/Taskify.Web/Program.cs`:
  - Register `TimeProvider.System`.
  - Add the named client `AddHttpClient("auth", c => c.BaseAddress = new Uri(ApiServiceName)).AddServiceDiscovery()`.
  - Keep `IdentityService` scoped.

  Update `src/Taskify.Web/Components/Pages/Home.razor`: `SelectUser` becomes async, and calls `await Identity.SignInAsync(user)` before navigating. Show the existing error alert on failure, with no new UI.
- [X] T077 [US3] In `src/Taskify.Web/Services/ApiClient.cs`:
  - Inject `IdentityService`. Typed clients are created in the circuit's scope (research R14).
  - Route every call through one private `SendAsync(HttpMethod, string path, object? body, CancellationToken)`, which sets `Authorization: Bearer {await identity.GetAccessTokenAsync()}`.
  - `AddCommentAsync`, `EditCommentAsync` and `DeleteCommentAsync` drop the author and requester parameters.

  Update the callers in `src/Taskify.Web/Components/Shared/TaskDetail.razor` and `CommentItem.razor`.
- [X] T078 [US3] In `src/Taskify.Web/Services/BoardHubClient.cs`, inject `IdentityService` and set `o.AccessTokenProvider = identity.GetAccessTokenAsync` in `WithUrl` options.
- [X] T079 [US3] Update `tests/Taskify.Web.Tests/` for the new `IdentityService`, `ApiClient` and `BoardHubClient` constructors. Add a helper `tests/Taskify.Web.Tests/Helpers/TestIdentity.cs` that creates a signed-in `IdentityService`, with a fake factory returning a fixed token. Update the existing endpoint tests in `tests/Taskify.Api.Tests/Endpoints/` and `Realtime/` to use `CreateClientAsAsync`. Then run `dotnet test Taskify.slnx`.
- [X] T080 [US3] Supply the signing key locally through `src/Taskify.AppHost/Program.cs`:
  - In `src/Taskify.AppHost/Program.cs`, add `var authKey = builder.AddParameter("auth-signing-key", secret: true);`, plus `.WithEnvironment("Auth__SigningKey", authKey)`, `.WithEnvironment("Auth__Issuer", "taskify")` and `.WithEnvironment("Auth__Audience", "taskify-api")` on `taskify-api`.
  - In `README.md` "Run locally", document `dotnet user-secrets set "Parameters:auth-signing-key" "<32+ random bytes, base64>" --project src/Taskify.AppHost` (FR-030).
- [X] T081 [US3] Supply the signing key in Azure through `infra/` and `.github/workflows/azure-dev.yml`:
  - In `infra/main.bicep`, add `@secure() param authSigningKey string` and pass it to the container-apps module.
  - In `infra/resources/container-apps.bicep`, add the secret `auth-signing-key` to `taskifyApiContainerApp`, plus env `Auth__SigningKey` (`secretRef: 'auth-signing-key'`), `Auth__Issuer` `taskify` and `Auth__Audience` `taskify-api`.
  - In `.github/workflows/azure-dev.yml`, step "Configure azd environment", add `azd env config set infra.parameters.authSigningKey "${{ secrets.AUTH_SIGNING_KEY }}"`.
  - In `docs/github-actions-setup.md`, document the new `AUTH_SIGNING_KEY` secret.
- [X] T082 [P] [US3] In `CONTEXT.md`, change the **Active User** definition to: "The User a browser session acts as, chosen at the landing screen and vouched for by the server through a short-lived identity. The server takes the caller from that identity, never from the request." Drop the sentence calling it a client-side concept (FR-031).
- [X] T083 [P] [US3] Write `docs/adr/0002-api-issued-identity.md` in the format of `.agents/skills/domain-modeling/ADR-FORMAT.md`. The API issues a 15-minute HMAC-signed JWT after the user picker; the caller comes from `sub`; a known limit is that anyone can pick any User; Entra ID was rejected as out of scope (D2).
- [X] T084 [US3] Update `README.md`: in "Features", keep "no login required" and add that the server issues and checks the selected User's identity.
- [X] T085 [US3] Validate US3: run [quickstart.md § P3](quickstart.md#p3--server-vouched-identity), steps 1–9, including a session longer than 15 minutes (SC-005).

**Checkpoint**: US1–US3 work. Identity cannot be altered per request.

---

## Phase 6: User Story 4 – Safe to run at scale in production (Priority: P4)

**Goal**: Live updates across instances, Blazor affinity, probes, production telemetry, no retries of changes, an internal API and a Task-move metric (FR-032–FR-040).

**Independent Test**: [quickstart.md § P4](quickstart.md#p4--safe-to-run-at-scale).

### Setup for User Story 4

- [X] T086 [US4] Add `PackageVersion`s to `Directory.Packages.props`:
  - `Microsoft.AspNetCore.SignalR.StackExchangeRedis` (10.0.x)
  - `Aspire.Hosting.Redis` (13.1.x, matching `Aspire.Hosting.PostgreSQL`)
  - `Azure.Monitor.OpenTelemetry.AspNetCore` (latest stable)
  - `Testcontainers.Redis` (4.15.0)
  - `Microsoft.Extensions.Diagnostics.Testing` (10.x)

  Reference them in `src/Taskify.Api/Taskify.Api.csproj`, `src/Taskify.AppHost/Taskify.AppHost.csproj`, `src/Taskify.ServiceDefaults/Taskify.ServiceDefaults.csproj` and `tests/Taskify.Api.Tests/Taskify.Api.Tests.csproj`. Add a `ProjectReference` from `tests/Taskify.Web.Tests` to `src/Taskify.ServiceDefaults` if it is not already transitive.
- [X] T087 [US4] Create `tests/Taskify.Api.Tests/Infrastructure/SharedRedis.cs`, the same pattern as `SharedPostgres`: `new RedisBuilder("redis:7").Build()`, started once per process, exposing `GetConnectionString()`.

### Tests for User Story 4 (write first, confirm they fail) ⚠️

- [X] T088 [P] [US4] Create `tests/Taskify.Api.Tests/Realtime/MultiInstanceTests.cs` (FR-032, SC-006):
  - Create two factories with `fixture.CreateFactoryAsync`, both with the same `ConnectionStrings:taskifydb` and `UseSetting("ConnectionStrings:redis", SharedRedis.ConnectionString)`.
  - A `HubTestClient` with a token joins board 1 on factory A. Over 20 iterations, `PATCH /api/tasks/1/status` through factory B alternates `Done` and `ToDo`, and A receives each `TaskMoved` within 2 s.
  - It must fail before T094, because there is no backplane yet.
- [X] T089 [P] [US4] Create `tests/Taskify.Api.Tests/Realtime/BroadcastFailureTests.cs` (spec edge case: the relay is unavailable):
  - The factory has `ConnectionStrings:redis` set to `127.0.0.1:1,abortConnect=false,connectTimeout=500`.
  - `PATCH /api/tasks/1/status` returns 200, and the database shows the new Status.
- [X] T090 [P] [US4] Create `tests/Taskify.Api.Tests/Observability/TaskMoveMetricTests.cs` (FR-039, SC-010):
  - Use `MetricCollector<long>` on meter `Taskify.Api`, instrument `taskify.tasks.moved`, resolved from `fixture.Factory.Services.GetRequiredService<IMeterFactory>()`.
  - N = 5 Status changes increase the total by exactly 5.
  - A move to the same Status adds 0.
- [X] T091 [P] [US4] Create `tests/Taskify.Web.Tests/Services/ResilienceTests.cs` (FR-037, SC-008):
  - Build a `HostApplicationBuilder`, call `AddServiceDefaults()`, and register `AddHttpClient("probe")` with a primary handler that counts calls and always returns 503.
  - `POST` is called exactly once. `GET` is called more than once.
  - Set short retry delays through `Configure<HttpStandardResilienceOptions>` so the test finishes in under 10 s.

### Implementation for User Story 4

- [X] T092 [P] [US4] Create `src/Taskify.Api/Observability/TaskifyMetrics.cs`, a singleton. Its constructor takes `IMeterFactory`, creates `Meter("Taskify.Api")` and `Counter<long> TasksMoved = meter.CreateCounter<long>("taskify.tasks.moved", unit: "{move}")`, and exposes `void TaskMoved() => TasksMoved.Add(1)`. Register it in `src/Taskify.Api/Program.cs` with `AddSingleton<TaskifyMetrics>()`.
- [X] T093 [P] [US4] Create `src/Taskify.Api/Observability/Log.cs`, a `static partial class Log`, with `[LoggerMessage]` methods:
  - `BroadcastFailed(ILogger, string message, int projectId, Exception)` at Warning.
  - `UnexpectedError(ILogger, Exception)` at Error.

  Use them in `UnexpectedExceptionHandler`.
- [X] T094 [US4] Turn on the backplane in `src/Taskify.Api/Program.cs`: when `builder.Configuration.GetConnectionString("redis")` is not empty, chain `.AddStackExchangeRedis(redisConnectionString, o => o.Configuration.ChannelPrefix = RedisChannel.Literal("taskify"))` to `AddSignalR()`. Pass the resolved string, not the name (research R15). Readiness gets no Redis health check.
- [X] T095 [US4] In `src/Taskify.Api/Services/TaskService.cs` and `CommentService.cs`:
  - Wrap every hub send in a private `Task BroadcastAsync(int projectId, Func<ITaskifyClient, Task> send)`. It awaits the send, catches any exception, and calls `Log.BroadcastFailed` without rethrowing.
  - In `MoveTaskAsync`, call `metrics.TaskMoved()` after `SaveChangesAsync`, only when the old Status differs from the new one.
  - Inject `TaskifyMetrics` and `ILogger<…>`.
- [X] T096 [P] [US4] In `src/Taskify.Migrator/Program.cs`, replace the three interpolated `logger.Log…` calls with `[LoggerMessage]` methods in a `static partial class MigratorLog` at the bottom of the file.
- [X] T097 [US4] Update `src/Taskify.ServiceDefaults/Extensions.cs` (research R20–R22):
  - `http.AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods())`.
  - In `ConfigureOpenTelemetry`, `.WithMetrics(m => m.AddMeter("Taskify.Api") …)`.
  - In `AddOpenTelemetryExporters`, `if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"])) builder.Services.AddOpenTelemetry().UseAzureMonitor();`.
  - Keep the OTLP branch as is.
- [X] T098 [US4] Rewrite the connection in `src/Taskify.Web/Services/BoardHubClient.cs` (research R15):
  - Inject `ServiceEndpointResolver`.
  - In `StartAsync`, resolve `https+http://taskify-api` with `GetEndpointsAsync(…, ct)`, take the first endpoint's `EndPoint` as a `UriEndPoint` URI, and build `new Uri(baseUri, "hubs/taskify")`.
  - Configure `o.Transports = HttpTransportType.WebSockets; o.SkipNegotiation = true;` while keeping `AccessTokenProvider`.
  - Delete the `IHttpMessageHandlerFactory` dependency.
  - Remove the named client `taskify-api-hub` from `src/Taskify.Web/Program.cs`.

  Add `tests/Taskify.Web.Tests/Helpers/TestServiceDiscovery.cs`, which returns a resolver from `new ServiceCollection().AddServiceDiscovery().BuildServiceProvider()`, and update the bUnit tests to use it instead of `NullHttpMessageHandlerFactory`.
- [X] T099 [US4] In `src/Taskify.AppHost/Program.cs`, add `var redis = builder.AddRedis("redis");` and on `taskify-api` add `.WithReference(redis).WaitFor(redis)`.
- [X] T100 [P] [US4] Create `infra/resources/redis.bicep` (research R16):
  - Resource `Microsoft.Cache/redisEnterprise`: name `redis-${environmentName}-${uniqueId}`, SKU `Balanced_B0`, and `highAvailability: highAvailability ? 'Enabled' : 'Disabled'` from a `bool` parameter.
  - Child `databases` named `default`: non-clustered policy, `accessKeysAuthentication: 'Enabled'`, TLS only, port 10000.
  - Output a `@secure()` connection string `'${host}:10000,password=${key},ssl=True,abortConnect=False'`, built from `listKeys().primaryKey`.
  - **Before merging**, confirm the SKU name, clustering-policy enum and access-key property against the current `Microsoft.Cache/redisEnterprise` Bicep schema with `az bicep build` and `az deployment group what-if` (plan risk).
- [X] T101 [US4] Wire Redis and the web replica minimum through `infra/main.bicep` and `infra/main.parameters.*.json`:
  - In `infra/main.bicep`, add module `redis` from `./resources/redis.bicep`, with `highAvailability: environmentName == 'prod'`. Pass `redis.outputs.connectionString` to the container-apps module.
  - Add `param webMinReplicas int`.
  - In `infra/main.parameters.prod.json`, set `webMinReplicas` to 1. In `infra/main.parameters.dev.json`, set it to 0 (FR-034).
- [X] T102 [US4] Update `infra/resources/container-apps.bicep` (research R17–R19):
  - **API**: `ingress.external: false`; secret `redis-connection-string`, with env `ConnectionStrings__redis` (`secretRef`).
  - **Web**: `ingress.stickySessions: { affinity: 'sticky' }`; `scale.minReplicas: webMinReplicas`.
  - **Both apps**: `activeRevisionsMode: 'Single'`, plus `probes` on the container:
    - Startup: `httpGet` `/alive` port 8080, `periodSeconds: 5`, `failureThreshold: 24`.
    - Liveness: `/alive`, `periodSeconds: 10`, `failureThreshold: 3`.
    - Readiness: `/health`, `periodSeconds: 10`, `failureThreshold: 3`.
  - Confirm that no `httpRouteConfigs` targets the API.

  Run `az bicep build --file infra/main.bicep`.
- [X] T103 [US4] Rewrite the smoke tests in `infra/hooks/postdeploy.sh`:
  - Check that `GET $WEB_URL/` returns 200 and the body contains `Jordan Rivera`, a seeded User.
  - Check that `GET $WEB_URL/health` returns 200.
  - Check that `curl` to `$API_URL/health` does **not** return 200 (FR-038).
  - Remove the old direct API health check.

  In `.github/workflows/azure-dev.yml`, step "Run smoke tests", pass `TASKIFY_WEB_URL` and `TASKIFY_API_URL` from `steps.deployment-urls.outputs`.
- [X] T104 [P] [US4] Write `docs/adr/0003-redis-signalr-backplane.md` and `docs/adr/0004-internal-api-ingress.md` in the format of `.agents/skills/domain-modeling/ADR-FORMAT.md`:
  - **0003**: Azure Managed Redis B0 as the backplane; WebSockets only with `SkipNegotiation` from the web app, so the API needs no affinity; Azure Cache for Redis is retiring 2028-09-30; Azure SignalR Service was considered (D3).
  - **0004**: internal ingress; smoke tests through the web app (D4).
- [X] T105 [US4] Update `README.md`, "Observability" and "Features" (FR-040):
  - In Azure, telemetry goes to Application Insights through the Azure Monitor OpenTelemetry distro.
  - The health endpoints are `/health` (readiness) and `/alive` (liveness), used as Container Apps startup, liveness and readiness probes.
  - Mention the `taskify.tasks.moved` metric.
  - Remove "Notifications for task and comment activity" from "Features".
  - Re-check the "scale-to-zero … under 5 s cold start" claim, and state only what is configured: dev scales to zero; prod keeps one web replica.
- [X] T106 [US4] Validate US4 locally: run [quickstart.md § P4 → Locally](quickstart.md#p4--safe-to-run-at-scale), steps 1–4.
- [ ] T107 [US4] Validate US4 in Azure dev: `azd provision` with `what-if` review, then deploy and run [quickstart.md § P4 → In Azure dev](quickstart.md#p4--safe-to-run-at-scale), steps 1–7. Record the Application Insights transaction screenshot and the probe-restart timing (SC-007, SC-009) in the PR description.

**Checkpoint**: All four stories are complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T108 [P] Add a line under the title of `specs/001-create-taskify/contracts/rest-api.md` and `specs/001-create-taskify/contracts/signalr-hub.md`: "Superseded by `specs/004-production-hardening/contracts/`".
- [X] T109 [P] Set **Status** in `docs/hardening-modernization-plan.md` to "Implemented by `specs/004-production-hardening/`". List the decisions that changed in implementation, which are research R6 and R15.
- [X] T110 Add the overall and critical-path coverage figures from the `test` job summary (gates from T022 and T023) to the PR description, as the constitution's Development Workflow requires ("Test evidence: coverage delta and test counts reported by CI").
- [ ] T111 Review `README.md` statement by statement against the repository and the deployed dev environment (SC-011). Fix any remaining false claim.
- [ ] T112 Run the acceptance scenarios in `specs/001-create-taskify/spec.md` against the dev deployment, with two browsers, and confirm no visible change (FR-041, SC-012).
- [X] T113 Run `dotnet csharpier format .`, `dotnet format --verify-no-changes`, `dotnet build Taskify.slnx -c Release` (S1541 and CA2016 clean) and `dotnet test Taskify.slnx --settings coverage.runsettings`. All must pass.

---

## Dependencies & Execution Order

### Phase dependencies

| Phase | Depends on | Blocks |
|---|---|---|
| 1 Setup | — | 2 |
| 2 Foundational (harness) | 1 | all stories |
| 3 US1 (P1) | 2 | US2–US4: their tests rely on the PostgreSQL-only suite and the single schema writer |
| 4 US2 (P2) | US1 | US3: request records, endpoints and `Result<T>` |
| 5 US3 (P3) | US2 | US4: internal ingress assumes protected endpoints, and the multi-instance test uses tokens |
| 6 US4 (P4) | US3 | 7 |
| 7 Polish | US4 | — |

The spec states that stories are built in order (spec, "Story order and dependencies"). Each story is still **accepted** independently through its own quickstart section.

### Inside each story

- Tests first, and they must fail. Then shared contracts (`Taskify.Shared`), then services, then endpoints and composition (`Program.cs`), then the web app, then infrastructure, then docs and validation.
- **US2**: T043 and T044 depend on T036–T041. T047 depends on T043. T048 depends on T044. T050 depends on T046–T049.
- **US3**: T071 depends on T070. T074 depends on T072. T077 and T078 depend on T075.
- **US4**: T094 must land before T088 passes. T098 depends on T097 (service discovery from ServiceDefaults). T101 depends on T100. T102 depends on T101.

## Parallel Opportunities

- **Phase 1**: T002 and T003.
- **US1 tests**: T010–T015, which are separate files. Implementation: T018 and T020 alongside T016 and T017.
- **US2 tests**: T028–T035. Shared contracts: T036–T040 and T042. Endpoints: T046 alongside T047 and T048, once the services exist. Web: T056 and T057. Docs and benchmarks: T059 and T060.
- **US3 tests**: T064–T068. Types: T069 and T070. Docs: T082 and T083.
- **US4 tests**: T088–T091. Then T092, T093, T096 and T100. Docs: T104.
- **Polish**: T108 and T109.

### Parallel example: User Story 2

```bash
# Tests, written together, all failing:
Task: "OpenAPI completeness test in tests/Taskify.Api.Tests/Contract/OpenApiContractTests.cs"
Task: "Hub contract test in tests/Taskify.Api.Tests/Contract/HubContractTests.cs"
Task: "Validation tests for every input operation in tests/Taskify.Api.Tests/Endpoints/ValidationTests.cs"
Task: "Error format tests in tests/Taskify.Api.Tests/Endpoints/ErrorFormatTests.cs"
Task: "Task list SQL test in tests/Taskify.Api.Tests/Data/TaskListQueryTests.cs"

# Shared contracts, together:
Task: "TaskifyJson in src/Taskify.Shared/TaskifyJson.cs"
Task: "Request records in src/Taskify.Shared/Requests/"
Task: "ITaskifyClient and message records in src/Taskify.Shared/Realtime/"
Task: "Result<T> in src/Taskify.Api/Results/Result.cs"
Task: "UnexpectedExceptionHandler in src/Taskify.Api/ErrorHandling/UnexpectedExceptionHandler.cs"
```

## Implementation Strategy

### MVP first (User Story 1)

1. Phase 1 Setup, then Phase 2 harness. Stop at T009: the fixture must work against real PostgreSQL.
2. Phase 3 US1. Stop and validate with the quickstart's P1 section, including the four deliberate breaks.
3. Merge. From here on, the pipeline is trustworthy for every later change.

### Incremental delivery

Merge each story as its own PR after its checkpoint, with the user stories, Constitution Check bullets, performance impact and coverage delta in the description, as the constitution requires:

US1 → US2 (API shape; no visible change) → US3 (identity; deploy needs the `AUTH_SIGNING_KEY` secret first) → US4 (infrastructure; review `what-if` in dev before prod).

## Notes

- `[P]` means a different file with no unfinished dependency. Tasks that edit the same file, such as `Program.cs` or `container-apps.bicep`, are sequential.
- Commit after each task or logical group. Each commit builds with warnings as errors.
- Never put a version on a `<PackageReference>`; versions belong in `Directory.Packages.props`.
- Stop at any checkpoint to validate the story with its quickstart section.
