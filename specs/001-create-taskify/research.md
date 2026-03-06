# Research: Create Taskify

**Phase**: 0 — Outline & Research  
**Date**: March 5, 2026  
**Plan**: [plan.md](plan.md)

---

## R-001 · .NET Aspire + Blazor Server + ASP.NET Core API + PostgreSQL: Solution Architecture

**Decision**: Single .NET solution (`Taskify.sln`) with five projects: `AppHost`, `ServiceDefaults`, `Shared`, `Api`, `Web`.

**Rationale**: This is the canonical .NET Aspire multi-project structure documented by Microsoft. `ServiceDefaults` provides shared OpenTelemetry, health checks, and service resilience configuration injected once and reused. `Shared` holds DTOs and enums to avoid duplicating types across `Api` and `Web` without creating a circular project reference. `AppHost` declares all resources (PostgreSQL container, API service, Blazor Web service) and wires them together via `WithReference` so service discovery is automatic in development.

**Alternatives considered**:
- *Separate repos for API and frontend*: rejected — unnecessary for a single-team Phase 1; Aspire's value is precisely co-located multi-service orchestration.
- *Blazor WebAssembly instead of Blazor Server*: rejected — SignalR state synchronization is natively available in Blazor Server without added complexity; WASM would require an additional API call layer for every real-time event and longer initial download time.
- *Minimal API controllers instead of attribute-routed controllers*: acceptable either way; attribute-routed controllers selected for easier contract testing (route naming is explicit) and better OpenAPI scaffolding.

---

## R-002 · Drag-and-Drop in Blazor Server: SortableJS Interop

**Decision**: Use [SortableJS](https://sortablejs.github.io/Sortable/) via a thin JavaScript interop shim (`sortable-interop.js`). The shim listens for SortableJS's `onEnd` event and invokes a [JSInvokable] .NET callback passing `{ taskId, fromColumn, toColumn }`. Column DOM elements map to `ColumnStatus` enum values via `data-column` attributes.

**Rationale**: Blazor Server streams UI diffs over a persistent SignalR connection. If drag-and-drop position tracking were done in C#, each mouse-move event would cross the WebSocket boundary, consuming bandwidth and making smooth animation impossible. SortableJS handles all animation at 60 fps on the browser side; only the final drop event crosses the boundary. This satisfies the 60 fps constitution budget.

**Alternatives considered**:
- *`blazor-sortable` NuGet package*: wraps SortableJS but adds an abstraction layer that is harder to test and less current than direct SortableJS usage. Rejected in favour of a focused hand-written shim of < 50 lines.
- *HTML5 native drag-and-drop events via `@ondragstart`/`@ondrop`*: fires on every drag position; would generate hundreds of SignalR messages per drag gesture. Rejected as constitution-violating (performance budget).
- *Telerik / MudBlazor drag list*: component library dependency is disproportionate for a single Kanban use case. Rejected.

**Integration notes**:
- SortableJS loaded from CDN in `App.razor` (fallback local copy under `wwwroot/lib/`).
- `sortable-interop.js` exports `initKanban(dotNetRef, boardElementId)` → returns a disposable JS handle.
- `KanbanBoard.razor` calls `initKanban` on `OnAfterRenderAsync(firstRender)` and disposes on component disposal.
- Column re-ordering is **not** animated by Blazor; Blazor receives the message, updates DB, then broadcasts via SignalR. SortableJS optimistically reorders DOM; if the SignalR echo arrives, Blazor re-renders are diffed and no-ops if data is already consistent.

---

## R-003 · SignalR Hub Design for Real-Time Board Updates

**Decision**: Single `TaskifyHub` class in `Taskify.Api`. Clients join a board-specific group on connect (`await Groups.AddToGroupAsync(connectionId, $"board-{projectId}")`). The API services call `IHubContext<TaskifyHub>` to broadcast events. Hub group membership is stateless (no persistent connection tracking in DB).

**Hub events (server → client)**:

| Event Name | Payload | Trigger |
|---|---|---|
| `TaskMoved` | `{ taskId, fromColumn, toColumn }` | `PATCH /api/tasks/{id}/status` |
| `TaskAssigned` | `{ taskId, assigneeId }` | `PUT /api/tasks/{id}` (assignee change) |
| `CommentAdded` | `{ taskId, comment: CommentDto }` | `POST /api/tasks/{id}/comments` |
| `CommentEdited` | `{ taskId, commentId, newText }` | `PUT /api/comments/{id}` |
| `CommentDeleted` | `{ taskId, commentId }` | `DELETE /api/comments/{id}` |

**Client hub proxy** (`BoardHubClient.cs` in `Taskify.Web`) wraps `HubConnection`, subscribes to all events, and exposes `IObservable<T>` streams or delegates consumed by `KanbanBoard.razor`.

**Rationale**: Single hub keeps routing simple. Project-scoped groups avoid broadcasting to uninterested clients. Keeping hub thin (no business logic) makes it easily unit-testable via `IHubContext<T>` mock.

**Alternatives considered**:
- *Polling instead of SignalR*: rejected — spec SC-004 requires < 3 second propagation; polling would need ≤ 1 s intervals, generating unnecessary load with 5 concurrent users even at this scale.
- *Separate hub per resource type (TaskHub, CommentHub)*: rejected — over-engineering for Phase 1 with one board in view at a time.

---

## R-004 · Entity Framework Core + Npgsql: Migration and Seed Strategy

**Decision**: EF Core code-first migrations. Database is seeded once via `DatabaseSeeder.cs` called from `Program.cs` on startup (`app.Services.MigrateAndSeedAsync()`). Seed is idempotent (upsert-style: check existence before insert). All five users and three projects with ~30 tasks are seeded.

**Rationale**: Code-first migrations give a full audit trail of schema evolution. Aspire's `AddNpgsqlDbContext` extension wires the connection string from service discovery automatically, so no hardcoded connection strings in `appsettings.json`.

**Alternatives considered**:
- *Dapper instead of EF Core*: rejected — EF Core migration tooling and `DbContext` scaffold remove boilerplate; Dapper would require hand-writing SQL for every query in Phase 1.
- *EF Core `HasData()` in `OnModelCreating`*: usable but inflexible (can't reference environment). `DatabaseSeeder` class is preferred to keep `DbContext` configuration-only.

---

## R-005 · "Current User" State in Blazor Server

**Decision**: A scoped `IdentityService` (registered as `IIdentityService`) holds the selected `UserDto` for the lifetime of the circuit (browser tab session). `UserSelection.razor` calls `IdentityService.SetUser(user)` and then navigates to `/projects`. Any component in the tree can inject `IIdentityService` to read the active user. On page refresh the circuit is torn down and the user must re-select (spec assumption).

**Rationale**: Blazor Server's scoped DI corresponds to one browser session/circuit — exactly the lifetime we need. No cookies, no JWT, no persistent session store. Simple and honest for a no-auth prototype.

**Alternatives considered**:
- *Browser `localStorage` via JS interop to survive refresh*: rejected — spec explicitly states no persistence across refresh.
- *Singleton service*: rejected — singleton would share state across browser tabs/sessions.
- *`CascadingParameter`/`CascadingAuthenticationState`-style provider*: viable but heavier. Scoped service is simpler and equally testable.

---

## R-006 · bUnit Testing Pattern for Blazor Server Components

**Decision**: bUnit 1.x with TestContext; API calls mocked via `Moq`; `IIdentityService` injected from test fixtures. SignalR hub client (`IBoardHubClient`) mocked so components under test can trigger fake real-time events.

**Coverage strategy**:
- `UserSelection.razor`: verifies all five users render; clicking a user calls `IdentityService.SetUser` and triggers navigation.
- `KanbanBoard.razor`: verifies four columns render; card in correct column; own cards get CSS class `card--mine`; drag-drop `onEnd` callback triggers API call + `IHubContext` broadcast (via integration test).
- `CommentItem.razor`: edit/delete buttons visible for own comments, absent for others'.

**Rationale**: bUnit renders Blazor components in-process without a browser, enabling fast CI execution. Combined with `Testcontainers.PostgreSQL` for integration tests, coverage can hit 95% on critical paths without E2E flakiness.

**Alternatives considered**:
- *Playwright / Selenium E2E*: retained as a future option but too slow and infrastructure-heavy for Phase 1 critical-path coverage.
- *Custom test doubles instead of Moq*: acceptable; Moq chosen for expressiveness and team familiarity.

---

## R-007 · WCAG 2.1 AA and Design Token Strategy

**Decision**: CSS custom properties (`--color-primary`, `--color-card-mine`, `--spacing-4`, etc.) defined in `tokens.css` imported globally. All components reference tokens only — no raw hex colors or magic spacing numbers in component stylesheets. `axe-core` loaded in development builds only (`#if DEBUG` in `App.razor`) and logs violations to browser console automatically on each render.

**Card color differentiation**: Cards assigned to the active user receive the CSS class `card--mine` which applies `background-color: var(--color-card-mine)`. Color choice must pass 4.5:1 contrast ratio against card text per WCAG 1.4.3. Suggested: `#E0F2FE` (sky-100) for "mine" vs `#FFFFFF` for others.

**Rationale**: Token-based styling enables theme changes without touching components. axe-core gives continuous a11y signal during development at zero cost. WCAG AA compliance is a constitution non-negotiable.

**Alternatives considered**:
- *MudBlazor or Radix Blazor component library*: would provide accessibility out-of-the-box but adds a large dependency for Phase 1. Manual WCAG compliance with axe-core validation is preferred for minimal-footprint MVP.

---

## R-008 · Performance Benchmarking in CI

**Decision**: `BenchmarkDotNet` benchmark project (`Taskify.Benchmarks`) measures p95 API response times for task reads and writes against a Testcontainers PostgreSQL instance. `dotnet-benchmark-github-action` posts results as PR comments. A custom GitHub Actions step fails the build if any measured p95 exceeds the constitution budget.

**Alternatively in Phase 1**: k6 load test script added to `.specify/benchmarks/` that runs in CI as a smoke check (5 VUs, 30s) and generates a `summary.json` checked against thresholds.

**Rationale**: Automated performance gates prevent regression without manual micro-benchmarking. Constitution Principle IV is a hard requirement, not aspirational.

---

## R-009 · Notifications Endpoint

**Decision**: `GET /api/notifications` returns an empty array in Phase 1. The endpoint exists and returns 200 OK to fulfil the spec's "REST API layer exposing Notifications" requirement, but no notification-generation logic is implemented. All real-time updates are delivered via SignalR hub events (see R-003). A `TODO` comment marks the endpoint for Phase 2 extension.

**Rationale**: The spec does not define notification business rules beyond listing the endpoint. Stubbing prevents contract breakage in Phase 2 while avoiding over-engineering in Phase 1.

---

## R-010 · .NET Aspire 10 — Typed HttpClient with Service Discovery (`Taskify.Web` → `Taskify.Api`)

### 1. NuGet packages required in `Taskify.Web.csproj`

No direct package reference to `Microsoft.Extensions.ServiceDiscovery` is required in `Taskify.Web` **if** the project already references `Taskify.ServiceDefaults`. The ServiceDefaults project (generated by the Aspire template) declares the transitive dependency and calls both `AddServiceDiscovery()` and `ConfigureHttpClientDefaults(http => http.AddServiceDiscovery())`. Confirm the reference exists:

```xml
<!-- Taskify.Web.csproj -->
<ItemGroup>
  <ProjectReference Include="..\Taskify.ServiceDefaults\Taskify.ServiceDefaults.csproj" />
</ItemGroup>
```

If `Taskify.Web` does **not** use ServiceDefaults (e.g. it is a standalone client), add the package explicitly:

```xml
<PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="10.*" />
```

### 2. `Taskify.AppHost/Program.cs` — declare resources and wire the reference

```csharp
var builder = DistributedApplication.CreateBuilder(args);

var api = builder.AddProject<Projects.Taskify_Api>("taskify-api");

builder.AddProject<Projects.Taskify_Web>("taskify-web")
    .WithExternalHttpEndpoints()
    .WithReference(api)          // injects services__taskify-api__https__0 env var
    .WaitFor(api);               // delays Web startup until API health check passes

builder.Build().Run();
```

Key facts confirmed from Aspire source (`ResourceBuilderExtensions.cs`):
- `AddProject<T>(name)` — the string `"taskify-api"` becomes the service-discovery key.
- `WithReference(IResourceBuilder<IResourceWithServiceDiscovery>)` calls `ApplyEndpoints` internally, which injects `services__taskify-api__https__0` and `services__taskify-api__http__0` environment variables into the Web project automatically.
- No `WithReference` overload requires specifying a scheme; all live endpoints are injected.

### 3. `Taskify.Web/Program.cs` — register the typed HttpClient

```csharp
// AddServiceDefaults() already calls AddServiceDiscovery() and
// ConfigureHttpClientDefaults(http => http.AddServiceDiscovery())
builder.AddServiceDefaults();

builder.Services.AddHttpClient<ITaskifyApiClient, TaskifyApiClient>(client =>
{
    // The hostname MUST exactly match the AppHost resource name ("taskify-api").
    // "https+http://" enables scheme-fallback: HTTPS preferred, HTTP as fallback.
    client.BaseAddress = new Uri("https+http://taskify-api");
});
```

If `AddServiceDefaults()` is **not** called (no ServiceDefaults project), register manually:

```csharp
builder.Services.AddServiceDiscovery();

builder.Services.AddHttpClient<ITaskifyApiClient, TaskifyApiClient>(client =>
{
    client.BaseAddress = new Uri("https+http://taskify-api");
})
.AddServiceDiscovery();   // per-client opt-in when not using ConfigureHttpClientDefaults
```

### 4. Exact service name string for `BaseAddress`

```
https+http://taskify-api
```

- The hostname segment (`taskify-api`) **must exactly match** the resource name string passed to `AddProject<T>()` in AppHost.
- The `https+http://` scheme prefix is the canonical Aspire multi-scheme form. It signals to the resolver that HTTPS endpoints are preferred, with HTTP as fallback. Lowercase kebab-case resource names are the convention (e.g. `"taskify-api"`, not `"Taskify.Api"`).
- The env vars injected by `WithReference` take the form `services__taskify-api__https__0=https://localhost:7234` and `services__taskify-api__http__0=http://localhost:5234`. The resolver reads both.

### 5. `AddServiceDiscovery()` — explicit vs. implicit

**When using ServiceDefaults (standard Aspire setup):** `AddServiceDiscovery()` is called inside `AddServiceDefaults()` alongside `ConfigureHttpClientDefaults(http => http.AddServiceDiscovery())`. This means **every** `HttpClient` registered after `AddServiceDefaults()` is automatically service-discovery-enabled. No separate call is needed.

Confirmed from the Aspire project template source (`src/Aspire.ProjectTemplates/templates/aspire-servicedefaults/Extensions.cs`):

```csharp
public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
    where TBuilder : IHostApplicationBuilder
{
    builder.Services.AddServiceDiscovery();           // registers IServiceEndpointProvider

    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();                   // wires resolver into every HttpClient
    });
    // ...
}
```

**When NOT using ServiceDefaults:** call `AddServiceDiscovery()` on `builder.Services` once globally, then either use `ConfigureHttpClientDefaults` or call `.AddServiceDiscovery()` on each individual `IHttpClientBuilder`.

### 6. Minimal typed client implementation

```csharp
public interface ITaskifyApiClient
{
    Task<IReadOnlyList<TaskDto>> GetTasksAsync(Guid projectId, CancellationToken ct = default);
    Task MoveTaskAsync(Guid taskId, MoveTaskRequest request, CancellationToken ct = default);
}

public sealed class TaskifyApiClient(HttpClient httpClient) : ITaskifyApiClient
{
    // BaseAddress is already "https+http://taskify-api" — set at registration time.
    // All paths MUST be relative. Never include scheme/host inside the typed client.

    public async Task<IReadOnlyList<TaskDto>> GetTasksAsync(
        Guid projectId, CancellationToken ct = default)
    {
        var response = await httpClient.GetAsync($"api/projects/{projectId}/tasks", ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<TaskDto>>(ct) ?? [];
    }

    public async Task MoveTaskAsync(
        Guid taskId, MoveTaskRequest request, CancellationToken ct = default)
    {
        var response = await httpClient.PatchAsJsonAsync(
            $"api/tasks/{taskId}/status", request, ct);
        response.EnsureSuccessStatusCode();
    }
}
```

Critical rule: **never** construct absolute URIs inside the typed client. The `HttpClient.BaseAddress` is set at registration time; the service discovery `DelegatingHandler` resolves the actual endpoint at request time. Building absolute URIs in the client bypasses service discovery entirely.

### Breaking changes vs. Aspire 8

| Area | Aspire 9 | Aspire 10 |
|---|---|---|
| Preferred scheme syntax | `http://taskify-api` (single scheme) | `https+http://taskify-api` (multi-scheme fallback) is canonical |
| `WaitFor` | Not available | `WaitFor(resource)` added — delays consumer start until dependency is healthy |
| `AddServiceDiscovery` opt-in | Explicit per-client call was common | Bundled into `ConfigureHttpClientDefaults` inside the project template; all clients opt-in by default |
| Package versioning | `9.0.x` GA | Float within major: `Version="10.*"` recommended |

## R-010b · SortableJS JS Interop Deep-Dive (.NET 10 Blazor Server)

*Supplements R-002. Contains copy-paste-ready code patterns for the full DotNetObjectReference + IJSObjectReference lifecycle.*

### 1. `DotNetObjectReference<T>` — Creation and Disposal

Create the reference once (lazily on first render, not in the constructor) and store it as a field. Dispose it **after** the JS-side handle is disposed, because JS may still invoke the .NET callback during the disposal window.

```csharp
// KanbanBoard.razor.cs  (code-behind, or @code block)
private DotNetObjectReference<KanbanBoard>? _dotNetRef;
```

Create in — and only in — `OnAfterRenderAsync`:

```csharp
_dotNetRef ??= DotNetObjectReference.Create(this);
```

**Never** create it in `OnInitializedAsync`. The component class may be instantiated on the server before the circuit is established; JS interop is not available until after the first render.

### 2. Storing and Disposing the `IJSObjectReference`

`initKanban` must return the SortableJS instance handle so it can be explicitly destroyed.

```csharp
private IJSObjectReference? _sortableModule;
private IJSObjectReference? _sortableHandle;
```

`_sortableModule` is the ES module import (preferred over inline script injection in .NET 8+/9).  
`_sortableHandle` is the object returned by `initKanban` in JS.

### 3. `[JSInvokable]` Callback Receiving `{ taskId, fromColumn, toColumn }`

```csharp
[JSInvokable]
public async Task OnTaskDropped(string taskId, string fromColumn, string toColumn)
{
    // fromColumn / toColumn are the data-column attribute string values.
    // Parse to enum here, not in JS, to keep the shim thin.
    if (!Enum.TryParse<ColumnStatus>(toColumn, ignoreCase: true, out var targetStatus))
        return;

    await TaskService.MoveTaskAsync(taskId, targetStatus);
    // SignalR broadcast is triggered server-side inside MoveTaskAsync.
}
```

The JS invocation uses `invokeMethodAsync` with positional arguments (not a single object). This avoids writing a custom JSON binder and is idiomatic in Blazor.

### 4. JavaScript `initKanban` Function Skeleton

`wwwroot/js/sortable-interop.js` — **ES Module** (referenced via `<script type="module">` or `IJSRuntime.InvokeAsync("import", ...)` pattern):

```javascript
// wwwroot/js/sortable-interop.js
import Sortable from '/lib/sortable/sortable.esm.js'; // local fallback

/**
 * @param {DotNetObjectReference} dotNetRef   – .NET component reference
 * @param {string}                boardId     – id of the board wrapper element
 * @returns {{ dispose: function }}            – disposable handle
 */
export function initKanban(dotNetRef, boardId) {
  const board = document.getElementById(boardId);
  if (!board) throw new Error(`Board element #${boardId} not found`);

  const instances = [];

  board.querySelectorAll('[data-column]').forEach(columnEl => {
    const instance = Sortable.create(columnEl, {
      group: 'kanban',          // shared group enables cross-column drag
      animation: 150,
      ghostClass: 'card--ghost',
      onEnd(evt) {
        const taskId     = evt.item.dataset.taskId;
        const fromColumn = evt.from.dataset.column;
        const toColumn   = evt.to.dataset.column;

        if (fromColumn === toColumn) return; // same-column reorder — ignore

        dotNetRef.invokeMethodAsync('OnTaskDropped', taskId, fromColumn, toColumn)
          .catch(err => console.error('[sortable-interop] OnTaskDropped failed', err));
      }
    });
    instances.push(instance);
  });

  return {
    dispose() {
      instances.forEach(s => s.destroy());
      instances.length = 0;
    }
  };
}
```

**CDN vs local**: `App.razor` loads SortableJS from CDN with a `<link rel="modulepreload">` and a local fallback via `<script onerror>`. The ES module path above uses the local copy under `wwwroot/lib/sortable/`.

### 5. `InvokeVoidAsync` Inside `OnAfterRenderAsync(firstRender)` — Race-Safe Pattern

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (!firstRender) return;

    // 1. Import the ES module once.
    _sortableModule = await JS.InvokeAsync<IJSObjectReference>(
        "import", "./js/sortable-interop.js");

    // 2. Create the .NET reference BEFORE passing it to JS.
    _dotNetRef ??= DotNetObjectReference.Create(this);

    // 3. Call initKanban; store the returned JS handle for later disposal.
    _sortableHandle = await _sortableModule.InvokeAsync<IJSObjectReference>(
        "initKanban", _dotNetRef, "kanban-board");
}
```

**Why this is race-safe**:
- `firstRender` guard ensures the call fires exactly once per circuit.
- `OnAfterRenderAsync` runs after the DOM is committed, so `document.getElementById(boardId)` is guaranteed to find the element.
- The `await` chain is sequential: the module is imported before `initKanban` is called, eliminating any module-not-yet-loaded race.
- **Do not** use `InvokeVoidAsync` here because you need the returned handle. `InvokeAsync<IJSObjectReference>` is the correct overload.

**.NET 10 note**: In .NET 10, `IJSRuntime` in Blazor Server is backed by `RemoteJSRuntime`. Dynamic module import (`"import"`) is fully supported. There is no change from .NET 8 for this pattern, but ensure `<script type="module">` tags are not placed inside `<body>` after `<blazor-script>` — order matters for ES module caching.

### 6. `IAsyncDisposable` Implementation — Correct Disposal Order

Dispose JS-side first (so SortableJS stops firing callbacks), then the .NET reference (so the GC can collect the component), then the module reference.

```csharp
public async ValueTask DisposeAsync()
{
    // 1. Stop SortableJS instances — prevents callbacks firing into a disposed component.
    if (_sortableHandle is not null)
    {
        try
        {
            await _sortableHandle.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (e.g. browser closed). Safe to swallow.
        }
        await _sortableHandle.DisposeAsync();
        _sortableHandle = null;
    }

    // 2. Release the .NET → JS module reference.
    if (_sortableModule is not null)
    {
        await _sortableModule.DisposeAsync();
        _sortableModule = null;
    }

    // 3. Release the DotNetObjectReference LAST — JS callbacks must not be
    //    possible after this point, which step 1 guarantees.
    _dotNetRef?.Dispose();
    _dotNetRef = null;
}
```

Register `IAsyncDisposable` in the `.razor` file directive:

```razor
@implements IAsyncDisposable
```

**.NET 10 note**: Blazor Server's `ComponentBase` does not call `DisposeAsync` on synchronous `IDisposable` implementations for `IJSObjectReference` — you **must** implement `IAsyncDisposable`, not `IDisposable`, or both. Mixing them without care causes the sync path to suppress the async path in certain DI disposal scenarios.

### 7. Memory-Leak Pitfalls and Mitigations

| Pitfall | Root Cause | Mitigation |
|---|---|---|
| Component held alive by JS closure | `dotNetRef` passed to JS is a rooted GC handle. JS closure keeps it alive even after Blazor navigation. | Call `_sortableHandle.dispose()` in `DisposeAsync` to drop the JS closure before releasing the .NET handle. |
| `IJSObjectReference` never disposed | Module import (`"import"`) allocates a JS object tracked by the Blazor JS runtime. Not disposing leaks the tracker. | Always `await _sortableModule.DisposeAsync()` in `DisposeAsync`. |
| `DotNetObjectReference` disposed before JS stops calling back | JS `onEnd` fires after .NET reference is GC'd → `ObjectDisposedException` logged in circuit. | Destroy SortableJS instances (step 1) before disposing `DotNetObjectReference` (step 3). |
| `OnAfterRenderAsync` called multiple times initialising multiple Sortable instances | Re-render triggered by parent `StateHasChanged` re-enters `OnAfterRenderAsync` with `firstRender == false` — yet if `_sortableHandle` is null-checked incorrectly, `initKanban` is called again. | Strict `if (!firstRender) return;` guard, **plus** null-check `_sortableHandle` as a double guard. |
| `JSDisconnectedException` swallowed silently hiding real bugs | Swallowing all exceptions from `InvokeVoidAsync` masks programming errors. | Catch **only** `JSDisconnectedException` and `TaskCanceledException` in disposal. Let all other exceptions propagate. |
| Sortable instances created before columns are in DOM | `OnAfterRenderAsync` called with `firstRender == true` but `querySelectorAll('[data-column]')` returns empty because columns are rendered conditionally. | Ensure columns render unconditionally on first render, or defer `initKanban` call with a `@ref` sentinel and a null-check in JS before calling `querySelectorAll`. |

### Complete Component Skeleton

```razor
@* KanbanBoard.razor *@
@implements IAsyncDisposable
@inject IJSRuntime JS
@inject ITaskService TaskService

<div id="kanban-board">
    @foreach (var column in Columns)
    {
        <div class="kanban-column" data-column="@column.Status.ToString()">
            @foreach (var task in column.Tasks)
            {
                <div class="card @(task.AssigneeId == CurrentUserId ? "card--mine" : "")"
                     data-task-id="@task.Id">
                    @task.Title
                </div>
            }
        </div>
    }
</div>

@code {
    [Parameter] public IReadOnlyList<KanbanColumnViewModel> Columns { get; set; } = [];
    [Parameter] public string CurrentUserId { get; set; } = "";

    private DotNetObjectReference<KanbanBoard>? _dotNetRef;
    private IJSObjectReference? _sortableModule;
    private IJSObjectReference? _sortableHandle;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        _sortableModule = await JS.InvokeAsync<IJSObjectReference>(
            "import", "./js/sortable-interop.js");

        _dotNetRef ??= DotNetObjectReference.Create(this);

        _sortableHandle = await _sortableModule.InvokeAsync<IJSObjectReference>(
            "initKanban", _dotNetRef, "kanban-board");
    }

    [JSInvokable]
    public async Task OnTaskDropped(string taskId, string fromColumn, string toColumn)
    {
        if (!Enum.TryParse<ColumnStatus>(toColumn, ignoreCase: true, out var targetStatus))
            return;

        await TaskService.MoveTaskAsync(taskId, targetStatus);
    }

    public async ValueTask DisposeAsync()
    {
        if (_sortableHandle is not null)
        {
            try   { await _sortableHandle.InvokeVoidAsync("dispose"); }
            catch (JSDisconnectedException) { }
            catch (TaskCanceledException)   { }
            await _sortableHandle.DisposeAsync();
            _sortableHandle = null;
        }

        if (_sortableModule is not null)
        {
            await _sortableModule.DisposeAsync();
            _sortableModule = null;
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }
}
```

### .NET 10 vs .NET 9 Gotchas

| Area | .NET 9 | .NET 10 |
|---|---|---|
| `IJSRuntime` null safety | Nullable enabled but `JS` injection always non-null at runtime. | Unchanged. |
| Static SSR + `OnAfterRenderAsync` | .NET 9 introduced **enhanced navigation** that can short-circuit interactive circuits. For `InteractiveServer` render mode (explicitly declared with `@rendermode InteractiveServer`), `OnAfterRenderAsync` behaves identically to .NET 8. | .NET 10 — behavior unchanged. Confirm the component declares `@rendermode InteractiveServer` — if inherited from `Routes.razor`, the parent must also be interactive. |
| `JSDisconnectedException` namespace | `Microsoft.JSInterop` | Unchanged. |
| ES Module dynamic import caching | `"import"` may be called multiple times on hot reload; each call returns the same cached module in the browser. | Unchanged — Blazor's `RemoteJSRuntime` does not re-import if the URL is identical. |
| `DotNetObjectReference` thread safety | Not thread-safe. | Unchanged — JS interop callbacks on a Blazor Server circuit are serialised by the `SynchronizationContext`; no lock required. |

---

## R-011 · SignalR Client in `Taskify.Web`: Aspire Service Discovery + `HubConnectionBuilder`

### 1. Service discovery URL resolution: `HubConnectionBuilder` vs `HttpClient`

**The core asymmetry**: Aspire service discovery is implemented as an `HttpClient` `DelegatingHandler` that intercepts outbound requests, resolves the logical service name to a concrete IP:port, and rewrites `Host` and `Uri` before the request leaves the process. `HubConnectionBuilder.WithUrl()` constructs its own internal `HttpClient` using `SocketsHttpHandler` directly — **this default handler has no knowledge of Aspire's service discovery**.

Consequence: passing `"https+http://taskify-api/hubs/taskify"` to `HubConnectionBuilder.WithUrl()` **without further configuration will throw `UriFormatException`** at connection start because `SocketsHttpHandler` rejects the non-standard `https+http` scheme.

**The bridge**: `HubConnectionBuilder` exposes `options.HttpMessageHandlerFactory` — a `Func<HttpMessageHandler, HttpMessageHandler>` whose return value becomes the innermost handler for SignalR's internal WebSocket and HTTP connections. By returning an `HttpMessageHandler` obtained from `IHttpMessageHandlerFactory.CreateHandler(namedClient)`, you inject the full service-discovery-aware pipeline into the SignalR connection.

```
HubConnectionBuilder.WithUrl(logicalUrl, options =>
{
    options.HttpMessageHandlerFactory = _ =>
        httpMessageHandlerFactory.CreateHandler("taskify-api");
})
```

`IHttpMessageHandlerFactory.CreateHandler(name)` returns the **handler chain** registered for the named `HttpClient`, which includes Aspire's `ServiceDiscoveryDelegatingHandler`. That handler understands `https+http://` and rewrites the URL before making the wire connection.

**Contrast with `HttpClient`**:

| | `HttpClient` via `IHttpClientFactory` | `HubConnectionBuilder` |
|---|---|---|
| Service discovery integration | Automatic via `AddServiceDiscovery()` on `IHttpClientBuilder` | Manual — must inject handler via `options.HttpMessageHandlerFactory` |
| Scheme `https+http://` supported natively | Yes, resolved by `ServiceDiscoveryDelegatingHandler` in the pipeline | No — throws unless handler override is provided |
| Environment variable source | `services__taskify-api__https__0` injected by Aspire `WithReference` | Same env vars, but only reachable if the SD handler is present in the chain |

### 2. NuGet packages in `Taskify.Web.csproj`

```xml
<!-- SignalR .NET client -->
<PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.*" />

<!-- Service discovery — already transitive via Taskify.ServiceDefaults;
     add explicitly only if ServiceDefaults is not referenced. -->
<!-- <PackageReference Include="Microsoft.Extensions.ServiceDiscovery" Version="10.*" /> -->
```

`Microsoft.AspNetCore.SignalR.Client` is **not** included in the ASP.NET Core shared framework for Blazor Server — it must be declared as an explicit `<PackageReference>`.

`Microsoft.Extensions.Http` (which provides `IHttpMessageHandlerFactory`) is included in the shared framework and requires no explicit reference.

### 3. Registration in `Taskify.Web/Program.cs`

```csharp
// AddServiceDefaults() registers AddServiceDiscovery() and
// ConfigureHttpClientDefaults(http => http.AddServiceDiscovery()).
builder.AddServiceDefaults();

// Register a named HttpClient whose handler chain includes service discovery.
// BaseAddress sets the logical service name; actual resolution is deferred to
// request time by the ServiceDiscoveryDelegatingHandler in the pipeline.
builder.Services.AddHttpClient("taskify-api-hub")
    .ConfigureHttpClient(c => c.BaseAddress = new Uri("https+http://taskify-api"));
// .AddServiceDiscovery() is NOT needed here if AddServiceDefaults() was already
// called above — ConfigureHttpClientDefaults applies it to every HttpClient.

// BoardHubClient is SCOPED: one instance per Blazor Server circuit (browser tab).
builder.Services.AddScoped<IBoardHubClient, BoardHubClient>();
```

### 4. `BoardHubClient.cs` — full constructor and connection setup

```csharp
// Taskify.Web/Hubs/BoardHubClient.cs
using Microsoft.AspNetCore.SignalR.Client;

namespace Taskify.Web.Hubs;

public interface IBoardHubClient : IAsyncDisposable
{
    HubConnectionState State { get; }
    event Func<Task>? StateChanged;

    Task StartAsync(CancellationToken ct = default);
    Task JoinBoardAsync(int projectId, CancellationToken ct = default);
    Task LeaveBoardAsync(int projectId, CancellationToken ct = default);

    event Func<TaskMovedEvent, Task>? OnTaskMoved;
    event Func<TaskAssignedEvent, Task>? OnTaskAssigned;
    event Func<TaskCreatedEvent, Task>? OnTaskCreated;
    event Func<CommentAddedEvent, Task>? OnCommentAdded;
    event Func<CommentEditedEvent, Task>? OnCommentEdited;
    event Func<CommentDeletedEvent, Task>? OnCommentDeleted;
}

public sealed class BoardHubClient : IBoardHubClient
{
    private readonly HubConnection _connection;
    private int _currentProjectId;

    public BoardHubClient(IHttpMessageHandlerFactory messageHandlerFactory)
    {
        // "https+http://taskify-api/hubs/taskify" is the logical URL.
        // The ServiceDiscoveryDelegatingHandler inside messageHandlerFactory
        // resolves it to the concrete https://localhost:{dynamicPort}/hubs/taskify
        // at connection-start time using the services__taskify-api__https__0 env var.
        _connection = new HubConnectionBuilder()
            .WithUrl("https+http://taskify-api/hubs/taskify", options =>
            {
                // Replace the default SocketsHttpHandler with the service-discovery-
                // aware handler chain registered for "taskify-api-hub".
                // The original handler passed by the runtime is discarded; the named
                // handler factory provides a fully configured replacement.
                options.HttpMessageHandlerFactory = _ =>
                    messageHandlerFactory.CreateHandler("taskify-api-hub");
            })
            .WithAutomaticReconnect([
                TimeSpan.Zero,           // immediate first retry
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(10),
                TimeSpan.FromSeconds(30)
            ])
            .ConfigureLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .Build();

        // Subscribe to server→client events BEFORE StartAsync.
        _connection.On<TaskMovedEvent>   ("TaskMoved",    e => OnTaskMoved?.Invoke(e)    ?? Task.CompletedTask);
        _connection.On<TaskAssignedEvent>("TaskAssigned", e => OnTaskAssigned?.Invoke(e) ?? Task.CompletedTask);
        _connection.On<TaskCreatedEvent> ("TaskCreated",  e => OnTaskCreated?.Invoke(e)  ?? Task.CompletedTask);
        _connection.On<CommentAddedEvent>  ("CommentAdded",   e => OnCommentAdded?.Invoke(e)   ?? Task.CompletedTask);
        _connection.On<CommentEditedEvent> ("CommentEdited",  e => OnCommentEdited?.Invoke(e)  ?? Task.CompletedTask);
        _connection.On<CommentDeletedEvent>("CommentDeleted", e => OnCommentDeleted?.Invoke(e) ?? Task.CompletedTask);

        // Re-join the current board group after any successful reconnect.
        // The server assigns a new ConnectionId on reconnect, so group membership
        // is lost and must be explicitly re-established.
        _connection.Reconnected += async _ =>
        {
            if (_currentProjectId != 0)
                await _connection.SendAsync("JoinBoard", _currentProjectId);

            StateChanged?.Invoke();
        };

        _connection.Reconnecting += _ => { StateChanged?.Invoke(); return Task.CompletedTask; };
        _connection.Closed       += _ => { StateChanged?.Invoke(); return Task.CompletedTask; };
    }

    public HubConnectionState State => _connection.State;

    // Components subscribe to this to trigger StateHasChanged when the connection
    // state changes (Reconnecting / Connected / Disconnected).
    public event Func<Task>? StateChanged;

    public event Func<TaskMovedEvent, Task>?    OnTaskMoved;
    public event Func<TaskAssignedEvent, Task>? OnTaskAssigned;
    public event Func<TaskCreatedEvent, Task>?  OnTaskCreated;
    public event Func<CommentAddedEvent, Task>?   OnCommentAdded;
    public event Func<CommentEditedEvent, Task>?  OnCommentEdited;
    public event Func<CommentDeletedEvent, Task>? OnCommentDeleted;

    /// <summary>
    /// Start the connection with retry until success or cancellation.
    /// WithAutomaticReconnect does NOT retry the initial connect — this must be done manually.
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await _connection.StartAsync(ct);
                return; // connected
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
            }
        }
    }

    public async Task JoinBoardAsync(int projectId, CancellationToken ct = default)
    {
        _currentProjectId = projectId;
        await _connection.SendAsync("JoinBoard", projectId, ct);
    }

    public async Task LeaveBoardAsync(int projectId, CancellationToken ct = default)
    {
        if (_currentProjectId == projectId)
            _currentProjectId = 0;
        await _connection.SendAsync("LeaveBoard", projectId, ct);
    }

    public async ValueTask DisposeAsync()
    {
        _currentProjectId = 0;
        await _connection.DisposeAsync();
    }
}
```

### 5. Service lifetime: scoped (circuit) vs singleton

**Use `AddScoped<IBoardHubClient, BoardHubClient>()`.**

| Lifetime | Behaviour | Verdict |
|---|---|---|
| `Scoped` | One `HubConnection` per Blazor Server circuit (browser tab session). Disposed when the circuit is torn down (tab closed / refresh). **Correct.** | ✅ Use this |
| `Singleton` | One `HubConnection` shared across all users and tabs. All board events from all projects broadcast to every user regardless of which board they're viewing. | ❌ Wrong |
| Transient / per-component | New connection created on every `@inject`. Components sharing a board get separate connections, sending redundant `JoinBoard` calls, and each receives its own copy of every event. | ❌ Wrong |

**Why not per-component?** A `KanbanBoard.razor` component and a hypothetical `BoardHeader.razor` on the same page both need to react to the same events. If each creates its own connection, both connect to the same board group — the server sends each event twice (once per connection). The scoped `IBoardHubClient` is a shared object that only one connection exists per circuit.

**Registration and injection**:
```csharp
// Program.cs
builder.Services.AddScoped<IBoardHubClient, BoardHubClient>();
```

```razor
@* KanbanBoard.razor *@
@inject IBoardHubClient HubClient
```

### 6. `HubConnectionState` lifecycle in a Blazor Server component

```razor
@* KanbanBoard.razor (relevant lifecycle section) *@
@implements IAsyncDisposable
@inject IBoardHubClient HubClient

@if (HubClient.State == HubConnectionState.Reconnecting)
{
    <div class="board-banner board-banner--warn" role="alert">
        Reconnecting to server…
    </div>
}
else if (HubClient.State == HubConnectionState.Disconnected)
{
    <div class="board-banner board-banner--error" role="alert">
        Connection lost. <button @onclick="ReconnectAsync">Retry</button>
    </div>
}

@code {
    [Parameter, EditorRequired] public int ProjectId { get; set; }

    private CancellationTokenSource _cts = new();

    protected override async Task OnInitializedAsync()
    {
        // Subscribe to hub events before starting so no events are missed.
        HubClient.OnTaskMoved    += HandleTaskMovedAsync;
        HubClient.OnTaskAssigned += HandleTaskAssignedAsync;
        HubClient.OnTaskCreated  += HandleTaskCreatedAsync;
        HubClient.OnCommentAdded += HandleCommentAddedAsync;

        // Re-render when connection state changes.
        HubClient.StateChanged += OnConnectionStateChangedAsync;

        // Start the connection if not already connected (another component on the
        // same circuit may have already started it).
        if (HubClient.State == HubConnectionState.Disconnected)
            await HubClient.StartAsync(_cts.Token);

        await HubClient.JoinBoardAsync(ProjectId, _cts.Token);
    }

    private async Task OnConnectionStateChangedAsync()
    {
        // HubConnection callbacks are not on the Blazor sync context.
        // InvokeAsync marshals back to the circuit's sync context.
        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleTaskMovedAsync(TaskMovedEvent e)
    {
        // Update local view model, then re-render.
        // (Implementation detail: find the task in the board columns and move it.)
        await InvokeAsync(StateHasChanged);
    }

    private async Task ReconnectAsync()
    {
        await HubClient.StartAsync(_cts.Token);
        await HubClient.JoinBoardAsync(ProjectId, _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        HubClient.OnTaskMoved    -= HandleTaskMovedAsync;
        HubClient.OnTaskAssigned -= HandleTaskAssignedAsync;
        HubClient.OnTaskCreated  -= HandleTaskCreatedAsync;
        HubClient.OnCommentAdded -= HandleCommentAddedAsync;
        HubClient.StateChanged   -= OnConnectionStateChangedAsync;

        // Leave the board group so the server stops sending events to this
        // connection (even though the connection stays alive for the circuit).
        try
        {
            await HubClient.LeaveBoardAsync(ProjectId, _cts.Token);
        }
        catch { /* circuit may already be gone */ }

        await _cts.CancelAsync();
        _cts.Dispose();
    }
}
```

**Why `InvokeAsync(StateHasChanged)` is required**: `HubConnection` event callbacks (`Reconnected`, `Reconnecting`, `Closed`, and `On<T>` handlers) are invoked on a thread-pool thread, outside the Blazor Server circuit's `SynchronizationContext`. Calling `StateHasChanged` directly from that thread throws `InvalidOperationException`. `InvokeAsync` marshals the call to the correct context.

### 7. Reconnect group re-subscription: why and how

SignalR hub groups are in-memory and connection-scoped on the server. When a WebSocket session drops, the server removes all group memberships for that `ConnectionId`. On reconnect, `WithAutomaticReconnect` opens a fresh physical connection — with a **new `ConnectionId`** — so the client is no longer in any group.

The `Reconnected` handler inside `BoardHubClient` handles this:

```csharp
_connection.Reconnected += async _ =>
{
    // _currentProjectId is set by the most recent JoinBoardAsync call.
    // If the user is still on a board page, re-join the group immediately.
    if (_currentProjectId != 0)
        await _connection.SendAsync("JoinBoard", _currentProjectId);

    StateChanged?.Invoke();
};
```

`_currentProjectId` is updated by `JoinBoardAsync` and cleared by `LeaveBoardAsync` / `DisposeAsync`. This ensures the correct group is rejoined even if the reconnect happens while the user is still on the board page.

### 8. `WithAutomaticReconnect` — caveats

- `WithAutomaticReconnect` retries **reconnection** only, not the **initial** `StartAsync`. The `StartAsync` retry loop in `BoardHubClient.StartAsync()` (section 4) covers the initial connect.
- After all retry delays are exhausted (`TimeSpan[]` is finite), the connection transitions to `Disconnected` and fires the `Closed` event. The component shows the retry UI at this point.
- An `IRetryPolicy` implementation can replace the fixed-array approach if jitter or time-bounded retry is needed:

```csharp
.WithAutomaticReconnect(new BoundedExponentialRetryPolicy(maxElapsed: TimeSpan.FromMinutes(5)))
```

### 9. `https+http://` scheme-fallback behaviour at runtime

In development (Aspire running locally), Aspire injects:
```
services__taskify-api__https__0=https://localhost:7xxxx  (Kestrel HTTPS)
services__taskify-api__http__0=http://localhost:5xxxx   (Kestrel HTTP)
```

The `ServiceDiscoveryDelegatingHandler` inspects the `https+http://` scheme, prefers the `https` endpoint, and rewrites the outbound URI to `https://localhost:7xxxx/hubs/taskify`. If no HTTPS endpoint is found, it falls back to the `http` entry. WebSocket transport (`wss://`) is handled transparently by the underlying `WebSocket.ConnectAsync` inside `HubConnection` — you never need to convert `https://` to `wss://` manually; SignalR does this internally.

In production (Azure Container Apps, Kubernetes), the pass-through provider returns the service name as a `DnsEndPoint`, and the platform's DNS resolves it. The `https+http://` scheme still works correctly because `AllowAllSchemes` defaults to `true`.

### 10. Differences from `IHttpClientFactory` approach

| Aspect | `HttpClient` typed client | `HubConnectionBuilder` |
|---|---|---|
| Service discovery | `.AddServiceDiscovery()` on `IHttpClientBuilder` | Manual: `options.HttpMessageHandlerFactory = _ => handlerFactory.CreateHandler("name")` |
| URL at registration | `client.BaseAddress = new Uri("https+http://taskify-api")` | `WithUrl("https+http://taskify-api/hubs/taskify", ...)` |
| Handler created | Per-request (pooled by `IHttpClientFactory`) | Once at `StartAsync` time; reused for the lifetime of the connection |
| WebSocket upgrade | N/A | Performed by `HubConnection` internally after HTTP negotiation |
| `Named client` required | Optional (typed clients embed name) | Required — `IHttpMessageHandlerFactory.CreateHandler(name)` demands a registered named client |

---

## R-012 · .NET Aspire 10 + EF Core 10 + PostgreSQL: Exact Wiring Pattern

**Sources**: NuGet package READMEs for `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` (v9.x) and `Aspire.Hosting.PostgreSQL` (v9.x); EF Core docs on applying migrations at runtime.

---

### 1. NuGet Packages — `Taskify.Api.csproj`

```xml
<!-- The single Aspire integration package. Transitively pulls in Npgsql.EntityFrameworkCore.PostgreSQL. -->
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.*" />

<!-- Required for `dotnet ef migrations add / update` tooling -->
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.*">
  <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  <PrivateAssets>all</PrivateAssets>
</PackageReference>

<!-- Snake-case column/table naming convention -->
<PackageReference Include="EFCore.NamingConventions" Version="10.0.0" />
```

**Do NOT add** `Npgsql.EntityFrameworkCore.PostgreSQL` separately — `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` already depends on it. Adding it separately risks version conflicts.

---

### 2. `AddNpgsqlDbContext<TContext>` — Signature and Source

| Property | Value |
|---|---|
| **Package** | `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` |
| **Namespace** | `Microsoft.Extensions.Hosting` (extension on `IHostApplicationBuilder`) |
| **Full signature** | `public static IHostApplicationBuilder AddNpgsqlDbContext<TContext>(this IHostApplicationBuilder builder, string connectionName, Action<NpgsqlEntityFrameworkCorePostgreSQLSettings>? configureSettings = null, Action<DbContextOptionsBuilder>? configureDbContextOptions = null) where TContext : DbContext` |

It automatically: enables connection pooling, configures retries, registers health checks, enables OpenTelemetry tracing and metrics.

---

### 3. `Taskify.Api/Program.cs` — DbContext Registration

```csharp
// connectionName MUST match the string passed to AddDatabase("...") in AppHost — see section 5.
builder.AddNpgsqlDbContext<TaskifyDbContext>(
    "taskifydb",                                         // connection name
    configureDbContextOptions: options =>
        options.UseSnakeCaseNamingConvention());         // from EFCore.NamingConventions
```

If you need to customise `NpgsqlEntityFrameworkCorePostgreSQLSettings` (e.g., disable health checks):

```csharp
builder.AddNpgsqlDbContext<TaskifyDbContext>(
    "taskifydb",
    configureSettings: settings => settings.DisableHealthChecks = false,
    configureDbContextOptions: options => options.UseSnakeCaseNamingConvention());
```

---

### 4. `Taskify.AppHost/Program.cs` — Resource Declaration

```csharp
// Aspire.Hosting.PostgreSQL package required in AppHost
var postgres = builder.AddPostgres("postgres");               // server resource name — arbitrary
var taskifyDb = postgres.AddDatabase("taskifydb");            // database name — THIS name is the connection name

var api = builder.AddProject<Projects.Taskify_Api>("taskify-api")
    .WithReference(taskifyDb)      // injects ConnectionStrings__taskifydb into Api
    .WaitFor(taskifyDb);           // blocks Api startup until Postgres container is ready
```

AppHost package:

```xml
<!-- Taskify.AppHost.csproj -->
<PackageReference Include="Aspire.Hosting.PostgreSQL" Version="10.*" />
```

---

### 5. Connection String Name Convention — The Critical Rule

> **The name passed to `AddNpgsqlDbContext("X")` must match the name passed to `AddDatabase("X")` in AppHost — NOT the server resource name from `AddPostgres("Y")`.**

`WithReference(taskifyDb)` causes Aspire to inject an environment variable:

```
ConnectionStrings__taskifydb=Host=localhost;Port=5432;Database=taskifydb;Username=postgres;Password=...
```

`AddNpgsqlDbContext("taskifydb")` reads `ConnectionStrings["taskifydb"]` from configuration.  
The `AddPostgres("postgres")` name (`"postgres"`) is the container/server resource name visible in the Aspire dashboard — it is irrelevant to the EF Core connection string lookup.

**Concrete mapping**:

| AppHost call | Connection string key injected | `AddNpgsqlDbContext` argument |
|---|---|---|
| `AddPostgres("postgres").AddDatabase("taskifydb")` | `ConnectionStrings__taskifydb` | `"taskifydb"` |
| `AddPostgres("pg").AddDatabase("mydb")` | `ConnectionStrings__mydb` | `"mydb"` |

---

### 6. Startup Migration Pattern — .NET 10

Use `CreateAsyncScope()` (available since .NET 6, preferred in .NET 10 for `IAsyncDisposable` correctness):

```csharp
// Taskify.Api/Program.cs — after builder.Build(), before app.Run()
var app = builder.Build();

// Run EF Core migrations on every startup (appropriate for dev/staging;
// use migration bundles or idempotent SQL scripts for production)
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaskifyDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
```

**Do NOT call `EnsureCreatedAsync()` before `MigrateAsync()`** — `EnsureCreatedAsync` bypasses the migrations history table and causes `MigrateAsync` to fail if any migrations exist.

From EF Core 9, `MigrateAsync()` uses a distributed advisory lock on PostgreSQL to serialise concurrent startup calls, making it safer for multi-instance deployments than EF Core 8 and earlier.

---

### 7. `UseSnakeCaseNamingConvention()` — Package

| Property | Value |
|---|---|
| **Package** | `EFCore.NamingConventions` |
| **Version for EF Core 9** | `9.0.0` |
| **Namespace** | `Microsoft.EntityFrameworkCore` (extension on `DbContextOptionsBuilder`) |
| **Effect** | Converts `PascalCase` entity/property names to `snake_case` column/table names at the provider level — no `[Column]` attributes needed |

Usage inside `OnConfiguring` or `AddNpgsqlDbContext`:

```csharp
options.UseSnakeCaseNamingConvention();
// Example: TaskItem.CreatedAt -> column "created_at" in table "task_items"
```
