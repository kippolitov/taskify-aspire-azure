# Feature Specification: Production Hardening

**Feature Branch**: `004-production-hardening`

**Created**: 2026-10-14

**Status**: Draft

**Input**: User description: "Harden Taskify so it can be trusted beyond a demo, without changing what a person sees or does on the board. Deliver it as four independently testable user stories, in this priority order. P1 – Trustworthy verification. P2 – Consistent, self-describing API. P3 – Server-vouched identity. P4 – Safe to run at scale in production. Constraints: no visible change to the board's behaviour or appearance; use the domain terms in CONTEXT.md; the project constitution applies. Out of scope: Notifications, keeping the Active User across a browser refresh, passwords or an external identity provider, optimistic concurrency on Tasks, private networking for the database, any UI redesign."

**Background**: [docs/hardening-modernization-plan.md](../../docs/hardening-modernization-plan.md) lists the 13 weak spots this feature resolves and the evidence for each. That document proposed four separate specs. This spec uses its alternative instead: one spec with four prioritised user stories.

## User Scenarios & Testing *(mandatory)*

The people in these stories are the **maintainer** (changes and releases Taskify), the **developer** (works on the API or the web app), the **team member** (one of the five Users, working on the board) and the **operator** (runs Taskify in production). A team member sees no change in any story. The board looks and behaves exactly as it does today.

### User Story 1 - Trustworthy verification (Priority: P1)

As the maintainer, I need automated tests that send real requests through the full request pipeline to the same database engine and version as production, and a pipeline that proves the deployable images still build, so that a green pipeline means the release works.

Today every data test runs against an in-memory stand-in for the database. Schema changes are applied in two places. The container image build is broken, and nothing in the pipeline notices. The pipeline collects coverage but never fails on it. The README describes testing that does not happen.

**Why this priority**: Every other story changes behaviour that must be proven by tests first (constitution, Principle II). Until the tests exercise the real pipeline and the real database engine, a green build proves little about P2–P4.

**Independent Test**: Run the test suite and the pipeline on a clean checkout. Then break the suite on purpose in four ways: an image that will not build, a coverage drop below 80%, an endpoint that accepts invalid input, a schema change that only one component knows about. Each break must turn the pipeline red.

**Acceptance Scenarios**:

1. **Given** a clean checkout and a container runtime, **When** the maintainer runs the test suite, **Then** every API test sends real requests through the full request pipeline to a database of the same engine and major version as production, and no test uses an in-memory stand-in.
2. **Given** a request to change a Task's Status to a value that is not one of the four Statuses, **When** it is sent through the full pipeline in a test, **Then** the test proves the request is rejected as invalid input, with the standard error body, and that nothing changed in the database.
3. **Given** a fresh database, **When** the system starts, **Then** exactly one component applies the schema changes, and it finishes before any service accepts requests. No other component changes the schema.
4. **Given** the schema-change component fails, **When** the system starts, **Then** the services do not start against the unmigrated database.
5. **Given** any proposed change, **When** the pipeline runs, **Then** both deployable container images are built, and a build failure in either one fails the pipeline.
6. **Given** a change that brings overall line coverage below 80%, **When** the pipeline runs, **Then** the pipeline fails and reports the coverage figure.
7. **Given** the README, **When** a reviewer checks each statement about testing against the repository, **Then** every statement is true.

---

### User Story 2 - Consistent, self-describing API (Priority: P2)

As a developer maintaining the API or the web app, I need every operation to validate input the same way, return the same error format, publish its success and error response shapes in the API description, and stop work when the caller cancels. Each request, response and real-time message must be defined once and shared by server and web app, so the two cannot drift.

**Why this priority**: The API is the boundary between the web app and the data. Uneven validation, different error formats and duplicated message definitions all cause bugs that tests in one project cannot see. P3 changes the shape of several requests, which is safer once each shape is defined in one place.

**Independent Test**: Read the published API description and check each operation. Send invalid input to every operation and compare the error bodies. Cancel a request part-way through. Load a Project's task list and check what is read from the database. Each check passes without P3 or P4.

**Acceptance Scenarios**:

1. **Given** any operation that accepts input, **When** it receives input that breaks a validation rule, **Then** it rejects the request with the same status and the same standard error body that every other operation uses for invalid input.
2. **Given** a request for something that does not exist (a Task, Project or Comment), **When** any operation handles it, **Then** the response uses the standard error body, the same as for every other "not found".
3. **Given** an unexpected failure inside any operation, **When** the caller receives the response, **Then** it uses the standard error body and reveals no internal details.
4. **Given** the published API description, **When** a developer reads any operation, **Then** it shows the shape of the success response and the shape of each error response the operation can return.
5. **Given** a Project with Tasks that have Comments, **When** the task list is requested, **Then** each Task carries its comment count, and no Comment content is read from the database to produce it.
6. **Given** a Task, **When** a developer changes its title and description, **Then** that is one operation, and changing its Assignee (including clearing it) is a separate operation. Neither operation touches the fields of the other.
7. **Given** a caller that cancels a request while it is being processed, **When** the cancellation arrives, **Then** the server stops the remaining work for that request and makes no partial change.
8. **Given** the real-time messages sent to board viewers (Task moved, Task assigned, Task created, Comment added, Comment edited, Comment deleted), **When** a developer looks for their names and shapes, **Then** each one is defined in exactly one place, used by both server and web app.
9. **Given** a request or response shape used by both server and web app, **When** a developer changes it, **Then** the change happens in one place, and a mismatch between the two sides fails the build rather than appearing at runtime.

---

### User Story 3 - Server-vouched identity (Priority: P3)

As a team member, when I pick myself on the landing screen, the system gives me an identity the server trusts, so that nobody can author, edit or delete Comments as someone else by altering a request.

The landing-screen picker stays: five predefined Users, no passwords. The difference is that today each request states who is calling, so anyone who edits a request can act as any User. After this story, the server confirms the choice once and then decides who the caller is.

**Why this priority**: This is the only story that closes a hole a team member could exploit. It comes after P1 and P2 because it changes request shapes and needs the real-pipeline tests to prove the rules hold.

**Independent Test**: Pick a User on the landing screen and use the board as normal. Then send requests directly, outside the web app: one with no identity, one that names another User as Author, one that edits another Author's Comment, one that subscribes to live updates with no identity. Each must be refused.

**Acceptance Scenarios**:

1. **Given** the landing screen, **When** a team member picks one of the five Users, **Then** the server confirms that User exists and issues an identity for that User. The screen, the picker and what happens next look exactly as they do today.
2. **Given** any request the web app sends, **When** it is inspected, **Then** no request body, address or query carries the caller's own user id. The server takes the caller from the identity.
3. **Given** a request that changes data (creates, changes or deletes a Task or a Comment) and carries no valid identity, **When** the server receives it, **Then** it rejects the request as unauthenticated and changes nothing.
4. **Given** an Active User and a Comment written by a different Author, **When** the Active User tries to edit or delete that Comment, **Then** the server refuses based on the identity and the Comment is unchanged.
5. **Given** an Active User adds a Comment, **When** the Comment is saved, **Then** its Author is the User named by the identity, whatever else the request contains.
6. **Given** a client with no valid identity, **When** it tries to subscribe to live board updates, **Then** the subscription is refused.
7. **Given** a request to issue an identity for a User who does not exist, **When** the server receives it, **Then** it refuses and issues nothing.

**Known, accepted limit**: Anyone can still pick any User on the landing screen. This story stops identity being changed per request. It does not prove who a person is.

---

### User Story 4 - Safe to run at scale in production (Priority: P4)

As the operator, I need the board to stay live and correct when services run more than one instance, unhealthy instances to be replaced automatically, production telemetry to reach the monitoring service, failed create or change requests never to be repeated automatically, the API to be reachable only from the web front end, and Task moves to be counted as a metric.

**Why this priority**: Production already allows several instances, but live updates and front-end sessions only work with one. Telemetry is configured but never sent. Health endpoints exist but nothing checks them. This story makes production behave as the README says it does. It comes last because it builds on the tested pipeline (P1) and the identity rules (P3) that protect the API.

**Independent Test**: In a production-like environment, run two API instances and several front-end instances. Move a Task through one and watch a board connected through the other. Make an instance unhealthy and watch it get replaced. Inject failures on a create request and on a read. Find a request trace in the monitoring service. Try to reach the API from outside the web front end.

**Acceptance Scenarios**:

1. **Given** two API instances and two board viewers on the same Project, each connected through a different instance, **When** one viewer changes a Task's Status, **Then** the other viewer sees the change live, the same as with one instance.
2. **Given** traffic spread over several front-end instances, **When** a team member keeps using the board, **Then** their session continues uninterrupted: the Active User stays selected and live updates keep arriving.
3. **Given** an instance that stops responding to health checks, **When** the platform detects it, **Then** that instance stops receiving traffic and is replaced with no operator action.
4. **Given** a starting instance that is not yet ready, **When** traffic arrives, **Then** none is sent to it until it reports ready.
5. **Given** a team member's action in production that passes through the web front end to the API, **When** the operator opens the production monitoring service, **Then** one request trace covering both services is visible.
6. **Given** a create request that fails with an error a retry might fix, **When** the web front end handles the failure, **Then** the request has been sent exactly once.
7. **Given** a read request that fails with an error a retry might fix, **When** the web front end handles the failure, **Then** it retries the read.
8. **Given** the production environment, **When** anyone tries to reach the API from outside, without going through the web front end, **Then** the API cannot be reached.
9. **Given** team members move Tasks between Statuses, **When** the operator looks at production metrics, **Then** a Task-move count is available and grows by one for each Status change.
10. **Given** the README, **When** a reviewer checks each statement about monitoring, health checks and Notifications against the deployed system, **Then** every statement is true.

---

### Edge Cases

- **Identity expires during a long session**: the web app renews the identity without asking the team member anything. The board never shows a sign-in prompt, and live updates keep arriving.
- **Live updates arrive after the identity has expired**: a live-update subscription opened with a valid identity stays open until it disconnects. Reconnecting needs a valid identity.
- **Old clients send fields that are no longer accepted**: a request that still names the caller (for example, as Author) must never decide who the caller is. The server ignores such a field or rejects the request, and never uses it.
- **Cancellation after the change is saved**: if the change was already saved, it stands, and board viewers are told about it. If it was not saved, nothing changes, and no live update about it is sent.
- **Message relay between API instances is unavailable**: changes are still saved and reads still work. Viewers connected through other instances may not get live updates until the relay recovers. The operator can see the condition in monitoring.
- **The front-end instance holding a viewer's session is replaced or removed**: the viewer reconnects to another instance. They may need to pick themselves again, the same as after a browser refresh today. Keeping the Active User across that is out of scope.
- **A change request fails after the server has already applied it**: the web front end does not repeat it automatically. Because no change is repeated, no duplicate Task or Comment can appear.
- **Schema change in progress during a deploy**: services of the new version do not start until the one schema-change component has finished.
- **Smoke tests after a deploy**: the API is no longer reachable from outside, so post-deploy checks reach it through the web front end.
- **Valid identity for a User later removed**: the five Users are fixed, so this cannot happen today. If it ever did, the server would reject the identity and not act for a missing User.

## Requirements *(mandatory)*

### Functional Requirements

**Verification (User Story 1)**

- **FR-001**: Every automated API test MUST send real requests through the complete server request pipeline. That covers routing, validation, error handling and serialization, ending at a real database.
- **FR-002**: The test database MUST be the same engine and major version as production. No in-memory or substitute database may be referenced by any test or benchmark project.
- **FR-003**: Each test class MUST run against a database whose schema was produced by the same schema changes production uses.
- **FR-004**: The test suite MUST include a test proving that invalid input is rejected through the full pipeline with the standard error body, and that the database is unchanged afterwards.
- **FR-005**: The test suite MUST verify live board updates by connecting as a real subscriber and observing the message.
- **FR-006**: Exactly one component MUST apply database schema changes. It MUST finish before any service accepts requests, both locally and in production. No other component may apply schema changes.
- **FR-007**: The pipeline MUST build both deployable container images on every proposed change and on every change merged to the main branch, and MUST fail if either build fails.
- **FR-008**: The pipeline MUST measure overall line coverage and MUST fail when it is below 80%.
- **FR-009**: Every user story in this spec MUST have at least one independent integration test (constitution, Principle II).
- **FR-010**: Every statement about testing in the README MUST be true of the repository.

**API consistency (User Story 2)**

- **FR-011**: Every operation MUST enforce its input rules through one shared validation mechanism. No operation may hand-write its own checks.
- **FR-012**: Every error response (invalid input, not found, refused, unauthenticated, unexpected failure) MUST use the standard error body. An unexpected failure MUST NOT expose internal details.
- **FR-013**: The published API description MUST show, for every operation, the shape of its success response and of each error response it can return.
- **FR-014**: The task list for a Project MUST return each Task's comment count without reading Comment content from the database.
- **FR-015**: Changing a Task's title and description MUST be one operation that sets both. Changing a Task's Assignee, including clearing it, MUST be a separate operation.
- **FR-016**: Every operation MUST stop its remaining work when the caller cancels. A cancelled request MUST leave no partial change.
- **FR-017**: Every request shape and response shape used by both the server and the web app MUST be defined once and shared by both.
- **FR-018**: The name and shape of every real-time message (Task moved, Task assigned, Task created, Comment added, Comment edited, Comment deleted) MUST be defined once and shared by the server and the web app.
- **FR-019**: Expected outcomes of an operation (success, not found, refused, invalid) MUST be represented explicitly, so that no operation signals an outcome through a missing or placeholder value.
- **FR-020**: Data formatting rules shared by requests, responses and real-time messages (such as how Statuses and field names are written) MUST be defined once.

**Identity (User Story 3)**

- **FR-021**: When a team member picks a User on the landing screen, the server MUST confirm that User exists and issue an identity for that User. If the User does not exist, the server MUST refuse.
- **FR-022**: The identity MUST be time-limited, MUST name exactly one User, and MUST be impossible for the caller to alter without the server detecting it.
- **FR-023**: No request MAY carry the caller's own user id in its body, address or query. The server MUST take the caller from the identity alone.
- **FR-024**: Every request that creates, changes or deletes data MUST be rejected as unauthenticated when it carries no valid identity.
- **FR-025**: Reads of Projects, Tasks and Comments MUST also require a valid identity. Only identity issuance and the User list shown on the landing screen remain open.
- **FR-026**: A new Comment's Author MUST be the User named by the identity.
- **FR-027**: Editing or deleting a Comment MUST be refused, based on the identity, when the caller is not its Author.
- **FR-028**: Subscribing to live board updates MUST require a valid identity.
- **FR-029**: The web app MUST obtain, hold and renew the identity for the session without any visible step for the team member.
- **FR-030**: The key that protects the identity MUST NOT be stored in the repository. It MUST be supplied as a secret in each environment.
- **FR-031**: The definition of Active User in CONTEXT.md MUST be updated to say that the server now vouches for it, and the decision MUST be recorded as an architecture decision record.

**Production (User Story 4)**

- **FR-032**: A live board update caused through any API instance MUST reach every viewer of that Project, whichever instance they are connected through.
- **FR-033**: A team member's front-end session MUST stay on the same front-end instance for its whole life, so that it survives traffic being spread over several instances.
- **FR-034**: Production MUST keep at least one front-end instance running at all times. Development environments may scale to zero.
- **FR-035**: Every service MUST be checked for startup, liveness and readiness. An instance that fails liveness MUST be replaced automatically. An instance that is not ready MUST receive no traffic.
- **FR-036**: Production traces, metrics and logs from every service MUST reach the production monitoring service, and a single request MUST be traceable across the web front end and the API.
- **FR-037**: The web front end MUST NOT automatically repeat a failed request that creates, changes or deletes data. It MAY retry a failed read.
- **FR-038**: In production the API MUST be reachable only from the web front end.
- **FR-039**: Every change of a Task's Status MUST increase a Task-move metric by one, visible in production monitoring.
- **FR-040**: Every statement in the README about monitoring, health checks and Notifications MUST be true. Notifications MUST NOT be listed as an available feature.

**Constraints on all stories**

- **FR-041**: The board's appearance and behaviour for a team member MUST be unchanged: the same screens, interactions, feedback and live updates.
- **FR-042**: The existing input rules (such as required fields and length limits) MUST stay as they are. This feature changes how the rules are enforced, not what they are.
- **FR-043**: Specs, code, tests and docs produced by this feature MUST use the domain terms in CONTEXT.md.

### Key Entities *(include if feature involves data)*

- **Active User identity**: the server-issued, time-limited proof that a browser session is acting as one specific User. It carries a reference to the User and an expiry, nothing more. It is issued after the landing-screen pick and checked on every request and live-update subscription. It is not stored by the server.
- **Real-time message**: a live board update sent to everyone viewing a Project (Task moved, Task assigned, Task created, Comment added, Comment edited, Comment deleted). Each has one shared definition of its name and contents.
- **Standard error body**: the single machine-readable format every operation uses to report an error. It states the kind of error, a human-readable title and, for invalid input, which fields failed and why.
- **Task-move metric**: a running count of Task Status changes, reported to production monitoring.
- **Task, Status, Project, User, Comment, Author** (existing, as defined in CONTEXT.md): unchanged. The comment count on a Task in the task list is derived, not stored.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of API tests run against a database of the production engine and major version, and zero projects reference an in-memory database stand-in.
- **SC-002**: Each of four deliberate breaks fails the pipeline, every time it is tried: an unbuildable image, coverage below 80%, an endpoint that accepts invalid input, and a schema change applied outside the one schema-change component.
- **SC-003**: 100% of operations in the API description show a success response shape and at least one error response shape.
- **SC-004**: For a Project with 500 Tasks averaging 20 Comments each, the task list reads no Comment content and responds within the constitution's read budget (p95 ≤ 200 ms).
- **SC-005**: Of every crafted request that tries to act as a different User (naming another Author, editing or deleting another Author's Comment, subscribing or changing data with no identity), 0% succeed.
- **SC-006**: In 20 out of 20 trials with two API instances, a Status change made through one instance appears to a viewer connected through the other within 2 seconds.
- **SC-007**: An instance made unhealthy stops receiving traffic and is replaced within 2 minutes, with no operator action.
- **SC-008**: In a fault-injection test, a failing create request is sent exactly once (0 duplicates created), and a failing read is retried at least once.
- **SC-009**: A request trace covering both the web front end and the API appears in production monitoring within 5 minutes of the request.
- **SC-010**: In a test that moves Tasks N times, the Task-move metric increases by exactly N.
- **SC-011**: A statement-by-statement review of the README finds 0 false claims about testing, monitoring, health checks or Notifications.
- **SC-012**: Every acceptance scenario of the original board specification ([specs/001-create-taskify/spec.md](../001-create-taskify/spec.md)) still passes, and a team member notices no difference in appearance or behaviour.

## Assumptions

- **Production database**: production runs PostgreSQL 16 on a managed server (`infra/main.bicep`). "Same engine and version" means PostgreSQL 16 in tests.
- **Standard error body**: this is the Problem Details for HTTP APIs format (RFC 9457), which the web app and tooling already understand.
- **Monitoring service**: the production monitoring service is the Application Insights resource already provisioned by the infrastructure. No new monitoring product is added.
- **Coverage gate**: the pipeline enforces the 80% overall line-coverage threshold. The constitution's 95% target for critical paths (identity, data persistence, Task changes) still applies to code this feature touches, and its coverage is reported. An automatic pipeline gate for 95% is not required by this spec.
- **Image builds**: the pipeline builds both images without publishing them on proposed changes. Publishing stays part of the existing deploy job.
- **Reads require identity** (FR-025): the input requires identity for changes and for live updates. Reads also require it because, after the landing-screen pick, the web app always has an identity, so this causes no visible change. The User list and identity issuance must stay open so the landing screen works.
- **Identity lifetime**: the identity is short-lived and renewed by the web app in the background. A team member is never asked to pick again because an identity expired.
- **Real-time message wire format**: message names and field names stay the same on the wire. Only where they are defined changes. Statuses are still sent as their names.
- **Contract changes**: the split of the Task update operation, and the removal of caller ids from Comment requests, supersede the matching parts of `specs/001-create-taskify/contracts/`. The web app is the only client, so no backward compatibility for outside callers is needed.
- **Front-end minimum**: keeping at least one front-end instance in production (FR-034) adds a small standing cost. This is accepted in exchange for no cold starts and no lost sessions.
- **Message relay**: delivering live updates across API instances needs a shared message relay. It adds one managed service to production and to local development, and that cost is accepted. Limiting the API to one instance was considered and rejected, because the acceptance criteria require two instances.
- **Story order and dependencies**: each story can be accepted on its own, but they are built in order. P1's real-pipeline test harness is used to prove P2–P4. P3's identity rules are what make it safe to close the API off from outside in P4.
- **Existing validation rules** (required fields, maximum lengths, the four Statuses) come from `specs/001-create-taskify/` and are not changed.
- **Out of scope**: Notifications (the existing placeholder stays), keeping the Active User across a browser refresh, passwords or an external identity provider, optimistic concurrency on Tasks, private networking for the database, and any UI redesign.
