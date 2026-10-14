# Research: Production Hardening

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-10-14

Every decision below was checked against current Microsoft documentation (or, for Testcontainers and Azure prices, the vendor's own pages) on 2026-10-14, for .NET 10, ASP.NET Core 10 and Aspire 13.1. Each entry names the source it was checked against. No `NEEDS CLARIFICATION` items remain.

Two findings change the design described in [docs/hardening-modernization-plan.md](../../docs/hardening-modernization-plan.md):

1. **R6 – validation across assemblies.** The validation source generator only covers types in the assembly that calls `AddValidation()`. The request records move to `Taskify.Shared`, so that project needs its own one-line registration.
2. **R14 – the SignalR backplane and sticky sessions.** A Redis backplane still needs sticky sessions unless clients use WebSockets only with negotiation skipped. `BoardHubClient` therefore switches to WebSockets with `SkipNegotiation`, and resolves the API address through service discovery, because `HttpMessageHandlerFactory` is not used for WebSockets.

---

## P1 – Trustworthy verification

### R1. Test harness: one PostgreSQL container per run, one database per test class

**Decision**: Host the API with `WebApplicationFactory<Program>`. Start one `postgres:16` container per test run with `Testcontainers.PostgreSql` (`new PostgreSqlBuilder("postgres:16").Build()`), held in a static, lazily started `SharedPostgres` holder. Each test class gets an `IClassFixture<TaskifyApiFixture>`. The fixture creates a uniquely named database on the shared container, applies the real EF Core migrations to it, and points the factory at it with `UseSetting("ConnectionStrings:taskifydb", …)`. Testcontainers' resource reaper removes the container when the test process exits.

**Rationale**:
- xUnit 2 has no assembly-level fixture.
- A collection fixture would share one container, but it puts every class in one collection, which runs them serially.
- A static holder plus a class fixture gives one container, isolated databases, and parallel test classes.
- Migrations run from the fixture, so tests use the same schema changes as production (FR-003).

**Alternatives considered**:
- **`Testcontainers.Xunit` `ContainerFixture` as a class fixture**: starts a container per class. That is slower, and the test count grows with every story.
- **`[assembly: AssemblyFixture]`**: needs xUnit v3 (`Testcontainers.XunitV3`). Moving to v3 is out of scope.
- **Respawn to reset one shared database between tests**: one more dependency and one more concept. A fresh database per class is enough.

**Sources**: [Testcontainers for .NET – xUnit](https://dotnet.testcontainers.org/test_frameworks/xunit_net/), [Testcontainers – PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/).

### R2. One component applies the schema

**Decision**: Delete `MigrateAsync` from `src/Taskify.Api/Program.cs`. `Taskify.Migrator` stays the only writer of the schema:
- **Locally**, the AppHost already runs it with `WaitForCompletion(migrate)` before the API and web app start.
- **In production**, `.github/workflows/azure-dev.yml` already runs it after `azd provision` and before `azd deploy`.
- **In tests**, the fixture applies migrations itself (R1).

**Rationale**: This satisfies FR-006 and acceptance scenario 1.4. If the migrator fails, `WaitForCompletion` blocks dependent resources locally, and a failed workflow step skips the deploy.

**Alternatives considered**:
- **Keep startup migration and rely on EF Core's migration lock**: still two writers, and it hides failures inside app startup.
- **A Container Apps job for migrations**: more infrastructure than the existing workflow step needs.

### R3. Container images in CI

**Decision**: Fix both Dockerfiles to copy `Directory.Packages.props` before `dotnet restore`. Add a CI job that runs `docker build` for both images, without pushing.

**Rationale**: `docker build -f src/Taskify.Api/Dockerfile .` on `main` fails with NU1015, confirmed on 2026-10-11, because Central Package Management versions are missing from the restore layer. A build-only job proves FR-007 cheaply.

**Alternatives considered**: SDK container publishing (`dotnet publish -t:PublishContainer`). It removes the Dockerfiles, but `azure.yaml` points at them, so changing it widens scope.

### R4. Coverage gate

**Decision**:
- Keep `coverlet.collector`, and run `dotnet test` with `--settings coverage.runsettings`.
- Merge the reports from both test projects with ReportGenerator, added as a local tool in `dotnet-tools.json`, and write a `JsonSummary`.
- A short CI step fails when the summary's line coverage is below 80, and prints the figure (FR-008).

**Rationale**: The collector does not enforce thresholds, and "overall" coverage needs the two test projects merged. ReportGenerator does both, and the repo already uses local tools.

**Alternatives considered**:
- **`coverlet.msbuild` with `/p:Threshold`**: applies per test project, not overall.
- **Codecov status checks**: an external service decides the gate.

### R5. Complexity enforcement

**Decision**: Enable SonarAnalyzer rule S1541 (method complexity) in `.editorconfig` as a warning. `TreatWarningsAsErrors` already turns warnings into build errors. Confirm when enabling that the rule's default threshold is 10; set it explicitly if it is not.

**Rationale**: The constitution requires complexity of 10 or less to be enforced in CI. SonarAnalyzer is already applied to every project, but S1541 is off by default.

---

## P2 – Consistent, self-describing API

### R6. Minimal APIs with built-in validation

**Decision**:
- Replace the controllers with one static endpoint class per resource, each with a `MapXxxEndpoints(this IEndpointRouteBuilder)` method that returns a route group.
- Call `builder.Services.AddValidation()` in the API.
- Because the request records live in `Taskify.Shared`, add a package reference to `Microsoft.Extensions.Validation` there, plus one extension method, `AddTaskifyContractValidation()`, that calls `AddValidation()` inside that assembly. The API calls both.
- Failed validation returns 400 with `HttpValidationProblemDetails`, formatted by the registered `IProblemDetailsService`.

**Rationale**:
- .NET 10 validates DataAnnotations on Minimal API parameters, including records, before the handler runs, so every `ModelState` check disappears (FR-011).
- The docs state that the source generator "creates metadata only for the assembly where `AddValidation` is called". Without the registration in `Taskify.Shared`, validation would silently not run for shared types; missing metadata produces no exception.
- `AddValidation` and the validation filter are stable. Only the underlying resolver APIs are experimental, and the plan does not use them.

**Alternatives considered**: Keep controllers and return `ActionResult<T>` with `[ProducesResponseType]` (D1, rejected). Validation, error formatting and OpenAPI shapes would stay hand-maintained per action.

**Sources**: [Validation in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/validation?view=aspnetcore-10.0), [Minimal APIs quick reference – validation](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis?view=aspnetcore-10.0#validation-support-in-minimal-apis), [What's new in ASP.NET Core 10 – Minimal APIs](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#minimal-apis).

### R7. Errors and expected outcomes

**Decision**:
- Call `AddProblemDetails()` and `UseExceptionHandler()`, and register one `IExceptionHandler` that logs the exception and writes a 500 Problem Details body with no exception detail (FR-012).
- Services return `Result<T>`, a success value or an `Error(ErrorKind Kind, string Detail)`, where `ErrorKind` is `NotFound`, `Forbidden` or `Unprocessable`.
- One `ToHttpResult()` maps a `Result<T>` to `Results<Ok<T>, ProblemHttpResult>`. Two variants cover `201 Created` and `204 No Content`.

**Rationale**: Every outcome is explicit (FR-019), and there is exactly one place that turns an outcome into a status code.

**Alternatives considered**:
- **Domain exceptions mapped by the exception handler**: control flow by exception, and the outcome is invisible in signatures.
- **A results library such as OneOf or ErrorOr**: a dependency for roughly 30 lines of code.

### R8. Error shapes in the OpenAPI document

**Decision**: Handlers return `TypedResults`, which describe success responses automatically. `ProblemHttpResult` carries no fixed status, so each endpoint also declares its error responses with `.ProducesProblem(status)`. Endpoints with a body add `.ProducesValidationProblem()`. A test fetches `/openapi/v1.json` from the factory, which runs in the Development environment, and asserts that every operation has a 2xx response and at least one 4xx or 5xx response (FR-013, SC-003).

**Rationale**: This is the smallest addition that makes error shapes visible. .NET 10 emits OpenAPI 3.1 by default.

**Alternatives considered**: Spell every error as a typed result, for example `Results<Ok<TaskDto>, NotFound<ProblemDetails>, UnprocessableEntity<ProblemDetails>>`. It is precise but verbose, and it duplicates what `Result<T>` already encodes.

**Source**: [What's new in ASP.NET Core 10 – OpenAPI](https://learn.microsoft.com/aspnet/core/release-notes/aspnetcore-10.0?view=aspnetcore-10.0#openapi).

### R9. One JSON configuration

**Decision**: `Taskify.Shared` exposes `TaskifyJson.Configure(JsonSerializerOptions)`, which applies Web defaults, camelCase and `JsonStringEnumConverter`, and `TaskifyJson.Options`, a ready instance. The API applies it through `ConfigureHttpJsonOptions` (the Minimal API JSON options) and `AddJsonProtocol`. `ApiClient` uses `TaskifyJson.Options` (FR-020).

**Rationale**: It replaces three hand-written blocks with one. Source-generated `JsonSerializerContext` is not needed, because Native AOT is out of scope.

### R10. Strongly typed hub

**Decision**:
- `ITaskifyClient` and the message records live in `Taskify.Shared/Realtime/`.
- `TaskifyHub : Hub<ITaskifyClient>`, and services inject `IHubContext<TaskifyHub, ITaskifyClient>`.
- `BoardHubClient` registers handlers with `nameof(ITaskifyClient.TaskMoved)` and the shared records, typed with `ColumnStatus`.
- Wire names and fields are unchanged; see [contracts/signalr-hub.md](contracts/signalr-hub.md).

**Rationale**: A mismatch becomes a compile error (FR-018). The message records are named `…Message` (for example `TaskMovedMessage`) so they never share a name with the interface methods.

**Alternatives considered**: The SignalR client source generator for hub proxies. It is not part of the stable package set and is one more generator to explain.

### R11. Task list query

**Decision**: Define one `Expression<Func<TaskItem, TaskDto>>` projection, used by both the list and single-task reads, that computes `t.Comments.Count` inside the query. Reads use `AsNoTracking` and drop `Include(t => t.Comments)`. A test asserts with `ToQueryString()` that the generated SQL contains a `count` subquery and selects no comment columns (FR-014).

**Rationale**: Today `Select(t => ToDto(t))` calls a C# method that EF Core cannot translate. Projection therefore happens in memory, after every comment row is loaded.

### R12. Cancellation and time

**Decision**:
- Minimal APIs bind `CancellationToken` to `HttpContext.RequestAborted`. Every handler passes it through the service into EF Core.
- Broadcasts sent after `SaveChangesAsync` use `CancellationToken.None`, so a saved change is always announced. This covers the "cancellation after the change is saved" edge case.
- Raise CA2016 (forward the `CancellationToken`) to a warning in `.editorconfig`.
- Register `TimeProvider.System` as a singleton. Tests use `FakeTimeProvider` from `Microsoft.Extensions.TimeProvider.Testing`.

**Rationale**: FR-016. A single `SaveChangesAsync` per operation is one transaction, so cancelling before it leaves no partial change.

### R13. Blazor additions

**Decision**: Add `@key="task.Id"` in the `TaskCard` loop. Put `[PersistentState]` on the public list properties that `Home` and `ProjectList` load in `OnInitializedAsync` (`Users ??= await …`).

**Rationale**:
- `[PersistentState]` (.NET 10) carries prerendered state into the interactive render, so the list is fetched once. Under Interactive Server, Data Protection protects that state.
- `@key` keeps Blazor's diff aligned with DOM nodes that SortableJS moves.
- Neither changes what a team member sees (FR-041).

**Source**: [Blazor prerendered state persistence](https://learn.microsoft.com/aspnet/core/blazor/state-management/prerendered-state-persistence?view=aspnetcore-10.0).

---

## P3 – Server-vouched identity

### R14 (identity). JWT issued by the API

**Decision**:
- `POST /api/auth/token` (anonymous) takes `{ userId }`, checks that the User exists, and returns `{ accessToken, expiresAt }`.
- The token is a JWT signed with HMAC-SHA256, using a key of at least 256 bits from configuration (`Auth:SigningKey`). It lives 15 minutes and carries `sub` (the user id), `exp`, `iss` and `aud`.
- The API validates it with `Microsoft.AspNetCore.Authentication.JwtBearer`, setting `MapInboundClaims = false` so `sub` is read as-is.
- A C# 14 extension property, `ClaimsPrincipal.UserId`, reads `sub`.
- Everything except the token endpoint and `GET /api/users` calls `RequireAuthorization()` (FR-025). `TaskifyHub` has `[Authorize]`.
- `CloseOnAuthenticationExpiration` stays off, so an open subscription outlives the token, as the spec's edge case requires.

**Signing key by environment**:

| Environment | Source |
|---|---|
| Local | Aspire secret parameter `auth-signing-key`, passed to the API as `Auth__SigningKey` |
| Tests | `UseSetting` |
| Azure | A `@secure()` Bicep parameter, fed from a GitHub secret into a Container Apps secret, the same pattern as the PostgreSQL connection string |

The key never enters the repository (FR-030).

**Web app**:
- `IdentityService`, scoped per circuit, holds the User, token and expiry. `GetAccessTokenAsync()` renews the token silently when less than 2 minutes remain, by calling the token endpoint through a separate named client (FR-029).
- `ApiClient` takes `IdentityService` in its constructor and sets the `Authorization` header in one private send helper.
- `BoardHubClient` passes `AccessTokenProvider = identity.GetAccessTokenAsync`. That provider is called before every HTTP request SignalR makes, so reconnects get a fresh token.

**Rationale**:
- The .NET SignalR client sends the token in the `Authorization` header, not the query string, so no `OnMessageReceived` hook is needed.
- `IHttpClientFactory` creates `DelegatingHandler`s in their own DI scope, so a handler cannot see the circuit's `IdentityService`. A typed client is created in the consumer's scope, so it can.

**Alternatives considered**:
- **A `DelegatingHandler` with `CircuitServicesAccessor` or an application-scope handler**: the documented workaround, but more moving parts than a header set in one method.
- **Cookie authentication**: browser-oriented; awkward for the server-to-server hub connection.
- **Microsoft Entra ID**: out of scope (D2).

**Sources**: [SignalR authentication and authorization](https://learn.microsoft.com/aspnet/core/signalr/authn-and-authz?view=aspnetcore-10.0), [SignalR client options](https://learn.microsoft.com/aspnet/core/signalr/configuration?view=aspnetcore-10.0#configure-client-options), [Call a web API from Blazor – accessing services outside the HttpClient's scope](https://learn.microsoft.com/aspnet/core/blazor/call-web-api?view=aspnetcore-10.0), [IHttpClientFactory – message handler scopes](https://learn.microsoft.com/dotnet/core/extensions/httpclient-factory#message-handler-scopes-in-ihttpclientfactory).

---

## P4 – Safe to run at scale

### R15. SignalR backplane and how the web app connects

**Decision**:
- **Backplane**: `AddSignalR().AddStackExchangeRedis(connectionString, o => o.Configuration.ChannelPrefix = RedisChannel.Literal("taskify"))`. The connection string is read from `ConnectionStrings:redis`, which Aspire's `WithReference(redis)` supplies locally and a Container Apps secret supplies in Azure. Pass the resolved value, not the name `"redis"`.
- **Web app connection**: `BoardHubClient` connects with `Transports = HttpTransportType.WebSockets` and `SkipNegotiation = true`. It first resolves the API's real base address with `ServiceEndpointResolver.GetEndpointsAsync("https+http://taskify-api", ct)`.
- **Failures**: a failed broadcast is caught, logged and counted in the services. A Redis outage therefore never fails a saved change (spec edge case). Readiness does not depend on Redis.

**Rationale**:
- Microsoft's scaling guide says sticky sessions are required with the Redis backplane, except when every client uses WebSockets only with `SkipNegotiation`. The API's only hub clients are the web app's servers, so WebSockets only is safe and the API needs no affinity.
- The docs also say `HttpMessageHandlerFactory` "isn't used for WebSocket connections". The current `https+http://…` hub URL therefore relies on non-WebSocket transports, and resolving the endpoint first fixes that.
- When Redis is down, SignalR drops messages and reconnects automatically, which matches the accepted edge case.

**Alternatives considered**:
- **Azure SignalR Service**: Microsoft's general recommendation for Azure-hosted SignalR, and no affinity is needed. Rejected by D3: it changes the hosting model.
- **Sticky sessions on the API plus negotiation**: needs cookie handling in the .NET client.
- **Capping the API at one replica**: contradicts SC-006.

**Sources**: [SignalR hosting and scaling](https://learn.microsoft.com/aspnet/core/signalr/scale?view=aspnetcore-10.0), [Redis backplane](https://learn.microsoft.com/aspnet/core/signalr/redis-backplane?view=aspnetcore-10.0), [ServiceEndpointResolver.GetEndpointsAsync](https://learn.microsoft.com/dotnet/api/microsoft.extensions.servicediscovery.serviceendpointresolver.getendpointsasync), [Aspire Redis deployment](https://learn.microsoft.com/dotnet/aspire/deployment/aspire-deploy/caching-integrations-deployment).

### R16. Azure Redis offering and cost

**Decision**: Azure Managed Redis, Balanced tier, SKU **B0**, non-clustered policy, access key in a Container Apps secret:

- **Dev**: one node, no high availability.
- **Prod**: two nodes, with high availability.
- **Locally**: `Aspire.Hosting.Redis` `AddRedis("redis")` runs a Redis container.

Microsoft Entra authentication for Redis is a follow-up, in line with the out-of-scope managed identity for PostgreSQL.

**Price check**: Azure retail prices API, East US 2, pay-as-you-go, queried on 2026-10-14. The public pricing page renders prices client-side. Monthly figures use 730 hours. Confirm in the [pricing calculator](https://azure.microsoft.com/pricing/calculator/?service=managed-redis) before approving.

| Option | Unit price | Monthly | Notes |
|---|---|---|---|
| Azure Managed Redis B0, one node (dev) | $0.016/h per instance | ≈ $11.68 | No SLA; for dev/test |
| Azure Managed Redis B0, two nodes, HA (prod) | 2 × $0.016/h | ≈ $23.36 | Two nodes are billed as two instances, the same metering as Azure Cache for Redis Standard |
| Azure Cache for Redis Basic C0 | $0.022/h | ≈ $16.06 | **Retiring 2028-09-30**; Microsoft advises not to start new work on it |
| Redis as a Container App (0.25 vCPU, 0.5 GiB, always on) | $0.000003–0.000024 per vCPU-s, $0.000003 per GiB-s | ≈ $6–20 before the free grant | Cheapest, but no SLA and patched by us |

**Rationale**:
- Azure Cache for Redis (Basic, Standard, Premium) retires on 2028-09-30, and Microsoft recommends Azure Managed Redis.
- B0 is the smallest Balanced SKU and is more than enough for pub/sub between a few replicas.
- Choosing the non-clustered policy means the backplane needs no cluster-aware configuration.

**Alternatives considered**:
- **Redis container app**: the lowest cost, but it adds an unmanaged stateful service to operate.
- **Azure Cache for Redis**: being retired.

**Sources**: [Azure Cache for Redis retirement FAQ](https://learn.microsoft.com/azure/azure-cache-for-redis/retirement-faq), [Basic, Standard and Premium versus Azure Managed Redis](https://learn.microsoft.com/azure/redis/migrate/migrate-basic-standard-premium-understand), [Azure Managed Redis pricing](https://azure.microsoft.com/pricing/details/managed-redis/), Azure retail prices API (`prices.azure.com`).

### R17. Blazor Server affinity and minimum replicas

**Decision**:
- **Web app**: `ingress.stickySessions.affinity: 'sticky'`, with `activeRevisionsMode: 'Single'` set explicitly, because affinity works only in single revision mode with HTTP ingress.
- **Replicas**: production `minReplicas: 1` for the web app; dev keeps `0` (FR-034).
- **API**: no affinity (R15).

**Source**: [Session affinity in Azure Container Apps](https://learn.microsoft.com/azure/container-apps/sticky-sessions).

### R18. Internal API ingress

**Decision**:
- **Ingress**: set `external: false` on the API. Its FQDN gains an `.internal.` segment, and requests from outside the environment get 404 from the environment proxy.
- **Web app**: keeps reading `services__taskify-api__https__0` from `ingress.fqdn`, which now resolves to the internal FQDN.
- **Smoke tests**: post-deploy checks call the web app. `GET /` must return 200 and list the seeded Users, which proves the web-to-API path. A probe of the API FQDN from the runner must not return a success.
- **Routing**: no environment-level HTTP route may target the API.

**Rationale**: FR-038. The docs warn that an environment HTTP route can still expose an internal app.

**Source**: [Communicate between container apps](https://learn.microsoft.com/azure/container-apps/connect-apps#container-app-location-fqdn), [Ingress overview](https://learn.microsoft.com/azure/container-apps/ingress-overview#external-and-internal-ingress).

### R19. Health probes

**Decision**: Both apps define three HTTP probes on port 8080:

| Probe | Path | Settings |
|---|---|---|
| Startup | `/alive` | `periodSeconds: 5`, `failureThreshold: 24`, giving 2 minutes to start |
| Liveness | `/alive` | `periodSeconds: 10`, `failureThreshold: 3` |
| Readiness | `/health` | `periodSeconds: 10`, `failureThreshold: 3` |

The API's `/health` includes the database check that the Aspire Npgsql integration adds. `/alive` runs only the `live`-tagged self check.

**Rationale**: FR-035 and SC-007. Liveness must not depend on the database, or a database outage would restart every replica.

**Source**: [Container Apps probe schema](https://learn.microsoft.com/azure/templates/microsoft.app/2025-07-01/containerapps), [Troubleshoot start failures – health probes](https://learn.microsoft.com/azure/container-apps/troubleshoot-container-start-failures).

### R20. Production telemetry

**Decision**: Add `Azure.Monitor.OpenTelemetry.AspNetCore` to `Taskify.ServiceDefaults`. Call `builder.Services.AddOpenTelemetry().UseAzureMonitor()` only when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set. Both container apps already receive it from Bicep. OTLP stays for local runs.

**Rationale**: FR-036. This is the pattern Microsoft documents for Aspire service defaults. Calling `UseAzureMonitor()` more than once throws, so it lives in exactly one place.

**Source**: [Use Application Insights for Aspire telemetry](https://learn.microsoft.com/dotnet/aspire/deployment/aspire-deploy/application-insights#use-the-azure-monitor-distro), [Azure Monitor Distro for .NET](https://learn.microsoft.com/dotnet/api/overview/azure/monitor.opentelemetry.aspnetcore-readme).

### R21. No automatic retry of changes

**Decision**: In `ServiceDefaults`, use `http.AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods())`.

**Rationale**: The method disables retries for POST, PATCH, PUT, DELETE and CONNECT. Reads keep three retries with exponential backoff (FR-037, SC-008). The docs name duplicated inserts as the reason to do this.

**Source**: [Build resilient HTTP apps – disable retries for HTTP methods](https://learn.microsoft.com/dotnet/core/resilience/http-resilience#remove-standard-resilience-handlers), [DisableForUnsafeHttpMethods](https://learn.microsoft.com/dotnet/api/microsoft.extensions.http.resilience.httpretrystrategyoptionsextensions.disableforunsafehttpmethods).

### R22. Task-move metric and logging

**Decision**:
- **Metric**: a `TaskifyMetrics` singleton creates a `Meter` named `Taskify.Api` through `IMeterFactory`, with a `Counter<long>` named `taskify.tasks.moved`. `TaskService` increments it once per Status change. `ServiceDefaults` adds `.AddMeter("Taskify.Api")`. Tests read it with `MetricCollector<long>` from `Microsoft.Extensions.Diagnostics.Testing` (SC-010).
- **Logging**: `[LoggerMessage]` source-generated methods replace interpolated log calls in the migrator and services.

### R23. Proving multi-replica delivery in tests

**Decision**: An integration test starts **two** `WebApplicationFactory` instances that share one PostgreSQL database and one Redis container from `Testcontainers.Redis`. A hub client connects to instance A, a Status change is made through instance B, and the test asserts that A receives `TaskMoved` within 2 seconds (FR-032, SC-006). The test hub client uses long polling through `TestServer.CreateHandler()`. The hub contract does not depend on the transport.

**Rationale**: SC-006 is checked on every CI run, not only by hand in Azure.
