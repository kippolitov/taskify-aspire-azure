# Tasks: Create Taskify

**Input**: Design documents from `/specs/001-create-taskify/`
**Prerequisites**: [plan.md](plan.md) · [spec.md](spec.md) · [research.md](research.md) · [data-model.md](data-model.md) · [contracts/rest-api.md](contracts/rest-api.md) · [contracts/signalr-hub.md](contracts/signalr-hub.md) · [quickstart.md](quickstart.md)

## Format: `[ID] [P?] [Story?] Description with file path`

- **[P]**: Can run in parallel (different files, no unresolved dependencies)
- **[US#]**: Maps to user story from spec.md (US1–US6)
- Setup and Foundational tasks carry no story label — they are prerequisites for all stories

---

## Phase 1: Setup

**Purpose**: Create the .NET 10 solution skeleton, all eight projects, shared NuGet configuration, and formatting tooling. No story work can begin until this phase is done.

- [ ] T001 Create `Taskify.sln` and all 8 projects via `dotnet new` (AppHost, ServiceDefaults, Shared, Api, Web; Api.Tests, Web.Tests, Benchmarks) per plan.md Step 1
- [ ] T002 [P] Add all NuGet packages (Aspire, EF Core 10, Npgsql, SignalR, bUnit 2.6.2, Testcontainers.PostgreSQL, CSharpier, SonarAnalyzer.CSharp, Coverlet, BenchmarkDotNet, k6) per research.md R-012 §1
- [ ] T003 [P] Wire all project references in each `.csproj` (Shared ← Api; Shared ← Web; ServiceDefaults ← Api; ServiceDefaults ← Web; Api ← Api.Tests; Web ← Web.Tests)
- [ ] T004 [P] Create `Directory.Build.props` with shared `<Nullable>enable</Nullable>`, `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>`, and `SonarAnalyzer.CSharp` package reference per plan.md Step 18
- [ ] T005 [P] Add `.editorconfig` with CSharpier formatting rules per plan.md Step 18

**Checkpoint**: Solution builds (`dotnet build Taskify.sln`) with zero errors before continuing.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Domain model, database, and Aspire wiring that every user story depends on. No story phase can start until this phase is complete.

**⚠ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T006 Define 5 domain entity classes (User, Project, TaskItem, Comment, Notification) in `src/Taskify.Api/Data/Entities/` per data-model.md Entities section
- [ ] T007 [P] Create all 5 shared DTO records (UserDto, ProjectDto, TaskItemDto, CommentDto, NotificationDto) in `src/Taskify.Shared/Dtos/` per contracts/rest-api.md request/response shapes
- [ ] T008 Configure `TaskifyDbContext` with Npgsql provider and `UseSnakeCaseNamingConvention()` in `src/Taskify.Api/Data/TaskifyDbContext.cs` per research.md R-012 §3
- [ ] T009 Generate EF Core migration `InitialCreate` and add `HasData` seed rows (5 users, 3 projects, 10 tasks) in `src/Taskify.Api/Data/TaskifyDbContext.cs` per data-model.md Seed Data section — use `MigrateAsync()` only, never `EnsureCreatedAsync()`
- [ ] T010 Configure `src/Taskify.Api/Program.cs` — register `AddNpgsqlDbContext<TaskifyDbContext>("taskifydb")`, call `MigrateAsync()` at startup, register `AddSignalR()`, map `/hubs/taskify` per research.md R-012 §5 (critical: connection name must match exactly)
- [ ] T011 Configure `src/Taskify.AppHost/Program.cs` — `AddPostgres("postgres").AddDatabase("taskifydb")` → api `WithReference(postgres).WaitFor(postgres)` → web `WithReference(api).WaitFor(api)` per plan.md Step 4
- [ ] T012 Configure `src/Taskify.Web/Program.cs` — `AddServiceDefaults()`, `AddHttpClient<ApiClient>` with base address `https+http://taskify-api` and `AddServiceDiscovery()` per research.md R-010 §4–§6

**Checkpoint**: `dotnet run --project src/Taskify.AppHost` launches; Aspire dashboard shows all three resources healthy; PostgreSQL migrations applied; seed data visible.

---

## Phase 3: User Story 1 — Select User Identity (Priority: P1) 🎯 MVP

**Goal**: A visitor can launch the app, see all five team members by name and role, click one, and be taken to the project list with that identity active.

**Independent Test**: Launch app → landing screen shows exactly 5 names (1 PM + 4 Engineers) → click any name → project list view opens with selected user's name shown → no password prompt appeared at any step. (spec.md US1 acceptance scenarios 1–3)

### Tests for User Story 1 (write first — must FAIL before implementation)

- [ ] T013a [US1] Write failing bUnit test for `UserSelection.razor` — assert 5 users rendered (names + roles) and `IdentityService.CurrentUser` set on click, in `tests/Taskify.Web.Tests/Components/UserSelectionTests.cs` per research.md R-014

### Implementation for User Story 1

- [ ] T013 [P] [US1] Implement `GET /api/users` and `GET /api/users/{id}` in `src/Taskify.Api/Controllers/UsersController.cs` per contracts/rest-api.md UsersController rows
- [ ] T014 [P] [US1] Implement `IdentityService` (Scoped, `CurrentUser` property, no auth) in `src/Taskify.Web/Services/IdentityService.cs` per research.md R-005
- [ ] T015 [US1] Implement `ApiClient` with typed `HttpClient` base and `GetUsersAsync()` / `GetUserAsync(id)` methods in `src/Taskify.Web/Services/ApiClient.cs` per research.md R-010 §6
- [ ] T016 [US1] Implement `UserSelection.razor` — fetch users from `ApiClient`, render list (name + role), on click set `IdentityService.CurrentUser` and navigate to `/projects` in `src/Taskify.Web/Components/Pages/UserSelection.razor` per spec.md US1

**Checkpoint**: User Story 1 fully functional and independently testable. Verify: all 5 users shown, clicking any name navigates to project list, identity persists until page refresh.

---

## Phase 4: User Story 2 — Browse Projects (Priority: P2)

**Goal**: After selecting identity, the user sees all three sample projects and can open any project's Kanban board.

**Independent Test**: Select any user → project list shows exactly 3 project names → click each project → correct board route opens for each. (spec.md US2 acceptance scenarios 1–3)

### Tests for User Story 2 (write first — must FAIL before implementation)

- [ ] T017a [US2] Write failing bUnit test for `ProjectList.razor` — assert 3 projects rendered and navigation invoked on click, in `tests/Taskify.Web.Tests/Components/ProjectListTests.cs` per research.md R-014

### Implementation for User Story 2

- [ ] T017 [P] [US2] Implement `GET /api/projects` and `GET /api/projects/{id}` in `src/Taskify.Api/Controllers/ProjectsController.cs` per contracts/rest-api.md ProjectsController rows
- [ ] T018 [US2] Add `GetProjectsAsync()` and `GetProjectAsync(id)` methods to `src/Taskify.Web/Services/ApiClient.cs`
- [ ] T019 [US2] Implement `ProjectList.razor` — guard redirect if no `IdentityService.CurrentUser`, fetch projects, render list, navigate to `/projects/{id}/board` on click in `src/Taskify.Web/Components/Pages/ProjectList.razor` per spec.md US2

**Checkpoint**: User Story 2 fully functional. Verify: project list shows 3 projects; clicking each navigates to the board route.

---

## Phase 5: User Story 3 — View Kanban Board (Priority: P3)

**Goal**: Opening a project shows a read-only Kanban board with four labeled columns, task cards in correct columns, and cards belonging to the active user visually highlighted.

**Independent Test**: Open any project → 4 columns ("To Do", "In Progress", "In Review", "Done") render in order → seed tasks appear in correct columns → cards assigned to the selected user are a distinct color; unassigned cards are neutral. (spec.md US3 acceptance scenarios 1–3)

### Tests for User Story 3 (write first — must FAIL before implementation)

- [ ] T020a [P] [US3] Write failing bUnit test for `TaskCard.razor` — assert title rendered, `.taskcard--mine` applied when assignee matches active user, `data-task-id` attribute present, in `tests/Taskify.Web.Tests/Components/TaskCardTests.cs` per research.md R-014
- [ ] T020b [US3] Write failing bUnit test for `KanbanBoard.razor` (read-only) — assert 4 columns render in order and seed task cards appear in correct columns, in `tests/Taskify.Web.Tests/Components/KanbanBoardTests.cs` per research.md R-014

### Implementation for User Story 3

- [ ] T020 [P] [US3] Implement `GET /api/projects/{id}/tasks` and `GET /api/tasks/{id}` in `src/Taskify.Api/Controllers/TasksController.cs` per contracts/rest-api.md TasksController rows
- [ ] T021 [US3] Add `GetTasksForProjectAsync(projectId)` and `GetTaskAsync(id)` to `src/Taskify.Web/Services/ApiClient.cs`
- [ ] T022 [P] [US3] Define all `--taskify-*` CSS custom properties (design tokens) and Kanban grid layout (4-column CSS grid) in `src/Taskify.Web/wwwroot/css/app.css` per quickstart.md Design Tokens section and research.md R-007
- [ ] T023 [P] [US3] Implement `TaskCard.razor` — render title, assignee name, `data-task-id` attribute, apply `.taskcard--mine` CSS class when `assignee.Id == IdentityService.CurrentUser.Id` in `src/Taskify.Web/Components/Pages/Shared/TaskCard.razor` per spec.md US3 and research.md R-010b §4
- [ ] T024 [US3] Implement `KanbanBoard.razor` — fetch tasks on load, group into 4 column buckets, render `<TaskCard>` components per column in `src/Taskify.Web/Components/Pages/KanbanBoard.razor` per spec.md US3

**Checkpoint**: User Story 3 fully functional. Verify: all 4 columns present; seed tasks visible; active-user cards are highlighted; board is read-only (no drag yet).

---

## Phase 6: User Story 4 — Drag and Drop Tasks Between Columns (Priority: P4)

**Goal**: Users can drag a task card to a different column; the move is persisted immediately and broadcast to all other viewers of the same board without a page reload.

**Independent Test**: Open board in two browser sessions as two different users → drag a card in session A → card appears in new column in session A immediately → card moves to same column in session B within 3 seconds without refresh. (spec.md US4 acceptance scenarios 1–3)

### Tests for User Story 4 (write first — must FAIL before implementation)

- [ ] T025a [US4] Write failing integration test for `PATCH /api/tasks/{id}/status` — assert LWW update persisted and 200 returned, in `tests/Taskify.Api.Tests/Controllers/TasksControllerTests.cs` per contracts/rest-api.md
- [ ] T025b [P] [US4] Write failing integration test for `TaskifyHub.JoinBoard` — connect two test clients, trigger status update, assert both receive `TaskMoved` event, in `tests/Taskify.Api.Tests/Hubs/TaskifyHubTests.cs` per contracts/signalr-hub.md
- [ ] T025c [P] [US4] Write failing bUnit test for `KanbanBoard.razor` drag callback — assert `OnTaskDropped` invoked with correct `(taskId, fromCol, toCol)` args and `UpdateTaskStatusAsync` called, in `tests/Taskify.Web.Tests/Components/KanbanBoardDragTests.cs` per research.md R-014

### Implementation for User Story 4

- [ ] T025 [US4] Add `PATCH /api/tasks/{id}/status` endpoint to `src/Taskify.Api/Controllers/TasksController.cs` per contracts/rest-api.md (delegates to TaskService)
- [ ] T026 [US4] Implement `TaskService` — `UpdateStatusAsync` using LWW `ExecuteUpdateAsync` pattern; broadcast `TaskMoved` via `IHubContext<TaskifyHub>` after update in `src/Taskify.Api/Services/TaskService.cs` per research.md R-013 and contracts/signalr-hub.md
- [ ] T027 [US4] Implement `TaskifyHub` — `JoinBoard(projectId)`, `LeaveBoard(projectId)` methods and all 6 S→C event definitions per contracts/signalr-hub.md Hub Methods and Client Events tables in `src/Taskify.Api/Hubs/TaskifyHub.cs` per research.md R-003
- [ ] T028 [P] [US4] Implement `sortable-interop.js` ES module — `Setup(dotNetRef, containerId)` export, `SortableJS` init with `onEnd` callback invoking `dotNetRef.invokeMethodAsync("OnTaskDropped", taskId, fromCol, toCol)` (positional args only) in `src/Taskify.Web/wwwroot/js/sortable-interop.js` per research.md R-010b §4–§7
- [ ] T029 [P] [US4] Implement `BoardHubClient` — `HubConnectionBuilder` with `IHttpMessageHandlerFactory` bridge, service-discovery URL `https+http://taskify-api/hubs/taskify`, exponential retry policy, `IAsyncDisposable` in `src/Taskify.Web/Services/BoardHubClient.cs` per research.md R-011 §1–§8
- [ ] T030a [US4] Add `UpdateTaskStatusAsync(id, status)` to `src/Taskify.Web/Services/ApiClient.cs` — required by `KanbanBoard.razor` drag handler (must precede T030)
- [ ] T030 [US4] Wire `BoardHubClient` into `KanbanBoard.razor` — subscribe to `TaskMoved` event, call `InvokeAsync(StateHasChanged)` (never `StateHasChanged()` directly), invoke `JoinBoard`/`LeaveBoard`, call `UpdateTaskStatusAsync` on drop, implement `IAsyncDisposable` to unsubscribe in `src/Taskify.Web/Components/Pages/KanbanBoard.razor` per research.md R-011 §6–§7

**Checkpoint**: User Story 4 fully functional. Verify: drag card → column updates immediately → second session reflects change within 3 s; drop to same column → no-op; LWW: concurrent moves → last writer wins.

---

## Phase 7: User Story 5 — Assign a User to a Task (Priority: P5)

**Goal**: From a task detail view, the active user can pick any of the five team members as the task's assignee; the board immediately reflects the new "mine" highlighting for the assigned user.

**Independent Test**: Open a task card with no assignee → select a user from the dropdown → close detail → card now shows that user's name → if the selected user matches the active identity, card switches to the "mine" highlight color. (spec.md US5 acceptance scenarios 1–3)

### Tests for User Story 5 (write first — must FAIL before implementation)

- [ ] T031a [US5] Write failing bUnit test for `TaskDetail.razor` — assert assignee dropdown renders all 5 users and `UpdateTaskAssigneeAsync` called on selection change, in `tests/Taskify.Web.Tests/Components/TaskDetailTests.cs` per research.md R-014

### Implementation for User Story 5

- [ ] T031 [US5] Add `PATCH /api/tasks/{id}/assignee` endpoint to `src/Taskify.Api/Controllers/TasksController.cs` per contracts/rest-api.md (delegates to TaskService)
- [ ] T032 [US5] Add `UpdateTaskAssigneeAsync(id, userId)` to `src/Taskify.Web/Services/ApiClient.cs` (`UpdateTaskStatusAsync` was added in T030a for Phase 6)
- [ ] T033 [US5] Implement `TaskDetail.razor` — display task title, render assignee dropdown populated from `GetUsersAsync()`, call `UpdateTaskAssigneeAsync` on selection change, display current assignee in `src/Taskify.Web/Components/Pages/TaskDetail.razor` per spec.md US5 and plan.md Step 12e

**Checkpoint**: User Story 5 fully functional. Verify: assignee picker shows all 5 users; selecting a user persists and reflects on the board card; re-assign replaces previous.

---

## Phase 8: User Story 6 — Comment on a Task Card (Priority: P6)

**Goal**: Users can add unlimited comments to any task; they can edit or delete only their own comments; other users' comments show no modification controls.

**Independent Test**: Open a task → add 3 comments → verify each shows author name → verify edit + delete controls appear only under own comments → edit one → verify updated text shown → delete one → verify removed → switch to different user identity → verify only that user's new comments are editable. (spec.md US6 acceptance scenarios 1–5)

### Tests for User Story 6 (write first — must FAIL before implementation)

- [ ] T034a [US6] Write failing integration tests for comment CRUD — assert `POST` adds comment attributed to author, `DELETE` by owner returns 204, `DELETE` by non-owner returns 403, in `tests/Taskify.Api.Tests/Controllers/CommentsControllerTests.cs` per contracts/rest-api.md
- [ ] T034b [P] [US6] Write failing bUnit test for `CommentItem.razor` — assert edit/delete controls present only when `AuthorId == CurrentUser.Id`, absent for other users, in `tests/Taskify.Web.Tests/Components/CommentItemTests.cs` per research.md R-014

### Implementation for User Story 6

- [ ] T034 [US6] Implement `CommentService` — `AddAsync`, `EditAsync`, `DeleteAsync` with ownership guard (`403` if `comment.AuthorId != requestingUserId`); broadcast `CommentAdded`/`CommentUpdated`/`CommentDeleted` events via `IHubContext<TaskifyHub>` in `src/Taskify.Api/Services/CommentService.cs` per contracts/signalr-hub.md and research.md R-013
- [ ] T035 [P] [US6] Implement `CommentsController` — `GET /api/tasks/{id}/comments`, `POST /api/tasks/{id}/comments`, `PUT /api/tasks/{taskId}/comments/{id}`, `DELETE /api/tasks/{taskId}/comments/{id}` per contracts/rest-api.md CommentsController rows in `src/Taskify.Api/Controllers/CommentsController.cs`
- [ ] T036 [P] [US6] Implement `NotificationsController` — `GET /api/users/{id}/notifications` and `PUT /api/users/{id}/notifications/{nId}/read` per contracts/rest-api.md in `src/Taskify.Api/Controllers/NotificationsController.cs`
- [ ] T037 [US6] Add comment CRUD methods (`GetCommentsAsync`, `AddCommentAsync`, `EditCommentAsync`, `DeleteCommentAsync`) to `src/Taskify.Web/Services/ApiClient.cs`
- [ ] T038 [P] [US6] Implement `CommentItem.razor` — display comment text, author name, timestamp, "edited" badge if `EditedAt != null`; render edit + delete controls only when `comment.AuthorId == IdentityService.CurrentUser.Id` in `src/Taskify.Web/Components/Pages/Shared/CommentItem.razor` per spec.md US6 and plan.md Step 12f
- [ ] T039 [US6] Add comment thread section to `TaskDetail.razor` — render `<CommentItem>` per comment in chronological order, add-comment text input + submit button, wire edit/delete callbacks in `src/Taskify.Web/Components/Pages/TaskDetail.razor` per spec.md US6

**Checkpoint**: User Story 6 fully functional. Verify: unlimited comments accepted; edit/delete appears only on own comments across all 5 user identities; edited badge shown after edit.

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Test coverage, performance validation, accessibility, and final code quality gates across all stories.

- [ ] T040 [P] Write bUnit 2.x tests for all 6 Blazor components (`BunitContext`, `ctx.Render<T>()`, `SetupModule` for sortable-interop.js, `VerifyInvoke`, `DisposeComponentsAsync`) in `tests/Taskify.Web.Tests/Components/` per research.md R-014 and plan.md Step 15
- [ ] T041 [P] Write Testcontainers.PostgreSQL integration tests for all 5 REST controller suites (users, projects, tasks, comments, notifications) covering happy paths + 403/404 cases in `tests/Taskify.Api.Tests/Controllers/` per contracts/rest-api.md and plan.md Step 16
- [ ] T042 [P] Write SignalR hub integration test — connect two clients, `JoinBoard`, trigger `TaskMoved`, assert both clients receive `BoardUpdated` event in `tests/Taskify.Api.Tests/Hubs/TaskifyHubTests.cs` per contracts/signalr-hub.md and plan.md Step 16
- [ ] T043 [P] Add BenchmarkDotNet API benchmarks (GET tasks, PATCH status baselines) in `tests/Taskify.Benchmarks/ApiBenchmarks.cs` per research.md R-008 and plan.md Step 17
- [ ] T044 [P] Add Coverlet CI coverage gates (`--threshold 80` overall, `--threshold 95` for `Services/`) and integrate axe-core WCAG 2.1 AA accessibility check per plan.md Step 17 and research.md R-007
- [ ] T045 Run `dotnet csharpier --check .` across all 8 projects; fix all formatting violations; confirm `dotnet build` passes with zero warnings per plan.md Step 18

**Checkpoint**: All tests green; coverage thresholds met; axe-core reports zero violations; benchmark baselines within p95 targets; CSharpier check passes in CI.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — start immediately
- **Phase 2 (Foundational)**: Requires Phase 1 complete — **BLOCKS all story phases**
- **Phase 3–8 (US1–US6)**: All require Phase 2 complete; execute in priority order or in parallel if staffed
- **Phase 9 (Polish)**: Requires all desired story phases complete

### User Story Dependencies

| Story | Depends On | Notes |
|---|---|---|
| US1 (P1) | Foundational | No story dependencies — pure entry point |
| US2 (P2) | US1 (IdentityService, navigation) | Needs identity to guard project list |
| US3 (P3) | US2 (project selection + navigation) | Reads tasks for a chosen project |
| US4 (P4) | US3 (board renders before drag is wired) | Adds drag + SignalR on top of read-only board |
| US5 (P5) | US3 (TaskDetail route + task fetch) | Assignee picker lives on TaskDetail |
| US6 (P6) | US5 (TaskDetail component exists) | Comments section added to TaskDetail |

### Parallel Opportunities Within Each Story

**US1**: T013 (controller) and T014 (IdentityService) are both marked `[P]` — different files, no dependencies. T015 (ApiClient) depends on T013 being callable. T013a (test) should be written before T016, but can be written in parallel with T013/T014.

**US2**: T017 (controller) is independent of web code — run alongside T017a (test) and T018/T019 prep work.

**US3**: T020 (controller), T020a/T020b (tests), T022 (CSS tokens), and T023 (TaskCard component) are all independent files — run all in parallel.

**US4**: The correct dependency order within the API is: T027 (hub class stub must exist first so `IHubContext<TaskifyHub>` resolves) → T026 (TaskService injects hub context) → T025 (controller delegates to service). T028 (JS shim), T029 (BoardHubClient), and T030a (ApiClient.UpdateTaskStatusAsync) are independent of the API chain — run in parallel while API tasks are being built. T030 (KanbanBoard wire-up) depends on T029, T030a, and T027 all being complete.

**US6**: T034a (integration test), T034b (bUnit test), T035 (CommentsController), and T036 (NotificationsController) are all independent files — run in parallel.

---

## Parallel Execution Examples

### Phase 1 — Full parallel after T001

```
T001 (solution + projects) — must complete first
↓
T002 [P]  T003 [P]  T004 [P]  T005 [P]
(NuGet)   (proj refs) (Build.props) (.editorconfig)
```

### Phase 5 — US3 parallel models + CSS

```
T020 [P]              T022 [P]          T023 [P]
(TasksController)     (CSS tokens)      (TaskCard.razor)
↓                     ↓                 ↓
                  T021 (ApiClient methods)
                       ↓
                  T024 (KanbanBoard.razor)
```

### Phase 9 — Full parallel polish

```
T040 [P]       T041 [P]       T042 [P]       T043 [P]       T044 [P]
(bUnit tests)  (API int. tests) (hub test)  (benchmarks)   (coverage+a11y)
↓                                                           ↓
                         T045 (CSharpier + final build check)
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational — **required before any story work**
3. Complete Phase 3: US1 — identity selection
4. **STOP and VALIDATE**: 5 users shown, clicking navigates, identity persists
5. Demo / deploy MVP

### Incremental Delivery

| Phase | Delivers | Demo Checkpoint |
|---|---|---|
| 1 + 2 | Running skeleton with seeded DB | Aspire dashboard healthy |
| + Phase 3 (US1) | Identity selection screen | User can "log in" |
| + Phase 4 (US2) | Project list | User reaches board route |
| + Phase 5 (US3) | Read-only Kanban board + highlighting | Board visible; "mine" cards highlighted |
| + Phase 6 (US4) | Drag-and-drop + real-time sync | Full collaborative board |
| + Phase 7 (US5) | Assignee picker | Full task management |
| + Phase 8 (US6) | Comments with ownership | Complete feature set |
| + Phase 9 | Tested, quality-gated build | Production-ready |

---

## Summary

| Metric | Value |
|---|---|
| Total tasks | 56 (T001–T045 + T013a, T017a, T020a–b, T025a–c, T030a, T031a, T034a–b) |
| Phase 1 (Setup) | 5 tasks (4 parallelizable) |
| Phase 2 (Foundational) | 7 tasks (1 parallelizable) |
| US1 tasks | 5 (1 test + 4 impl) |
| US2 tasks | 4 (1 test + 3 impl) |
| US3 tasks | 7 (2 tests + 5 impl) |
| US4 tasks | 10 (3 tests + 7 impl incl. T030a) |
| US5 tasks | 4 (1 test + 3 impl) |
| US6 tasks | 8 (2 tests + 6 impl) |
| Phase 9 (Polish) | 6 tasks (5 parallelizable) |
| Parallel opportunities | 27 tasks marked [P] |
| Suggested MVP scope | Phases 1–3 (US1): 17 tasks |
