# Data Model: Production Hardening

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-10-14

## Schema change: none

This feature adds, removes or alters no table, column, index or seed row. No EF Core migration is created. The existing `InitialCreate` migration stays the only one, so a check that the migration count is unchanged belongs to the P1 verification.

| Spec element | Why it needs no schema change |
|---|---|
| Comment count on a Task (FR-014) | Computed in the query (`t.Comments.Count`), never stored. |
| Active User identity (FR-021, FR-022) | A signed, time-limited token. The server stores nothing. |
| Task-move metric (FR-039) | An in-process counter exported to monitoring. |
| Real-time messages (FR-018) | Sent over SignalR and the Redis backplane, never persisted. |
| Splitting the Task update (FR-015) | Same columns, written by two operations instead of one. |

The persisted entities stay exactly as defined in `src/Taskify.Api/Data/` and [specs/001-create-taskify/data-model.md](../001-create-taskify/data-model.md): User, Project, TaskItem (the domain's **Task**) and Comment.

## Contract types (not persisted)

These types are what the feature adds. Request records, response DTOs and real-time messages live in `Taskify.Shared`. Server-only types live in `Taskify.Api`.

### Request records (`Taskify.Shared/Requests/`)

Validation attributes are enforced by `AddValidation()` before a handler runs (FR-011). The rules are the existing ones from `specs/001-create-taskify/` (FR-042).

| Record | Fields | Rules |
|---|---|---|
| `CreateTaskRequest` | `Title`, `Description?`, `AssigneeId?` | `Title` required, 1–300 characters. `AssigneeId`, when present, must name an existing User; otherwise 422, checked in the service. |
| `UpdateTaskRequest` | `Title`, `Description?` | `Title` required, 1–300 characters. `Description` replaces the current value, so `null` clears it. |
| `AssignTaskRequest` | `AssigneeId?` | `null` clears the Assignee. A value must name an existing User; otherwise 422. |
| `MoveTaskRequest` | `Status` | Required, and must be one of the four Statuses. An unknown name fails JSON binding with 400. An undefined number, such as `7`, which the string-enum converter would otherwise accept, is rejected by `[EnumDataType(typeof(ColumnStatus))]` with 400. |
| `AddCommentRequest` | `Text` | Required, 1–10,000 characters. **No author field**: the Author comes from the identity. |
| `EditCommentRequest` | `Text` | Required, 1–10,000 characters. **No requesting-user field.** |
| `TokenRequest` | `UserId` | Required, and must name an existing User; otherwise 404. |

**Removed fields**:
- `authorId` from `AddCommentRequest`.
- `requestingUserId` from `EditCommentRequest` and from the delete query string.
- `assigneeId` from the task update; it moves to `AssignTaskRequest`.

JSON binding ignores unknown properties, so an old client that still sends `authorId` is never trusted with it (spec edge case).

### Response DTOs (`Taskify.Shared/Dtos/`, existing)

`UserDto`, `ProjectDto`, `TaskDto`, `CommentDto` and `NotificationDto` are unchanged. One new DTO:

| Record | Fields |
|---|---|
| `TokenResponse` | `AccessToken` (string), `ExpiresAt` (`DateTimeOffset`, UTC) |

### Real-time messages (`Taskify.Shared/Realtime/`)

One record per message, sent through `ITaskifyClient` (FR-018). Wire names and field names are unchanged; see [contracts/signalr-hub.md](contracts/signalr-hub.md).

| Interface method | Payload record | Fields |
|---|---|---|
| `TaskMoved` | `TaskMovedMessage` | `TaskId`, `ProjectId`, `FromStatus` (`ColumnStatus`), `ToStatus` (`ColumnStatus`), `MovedAt` |
| `TaskAssigned` | `TaskAssignedMessage` | `TaskId`, `ProjectId`, `Assignee` (`UserDto?`) |
| `TaskCreated` | `TaskDto` | the existing DTO |
| `CommentAdded` | `CommentAddedMessage` | `TaskId`, `Comment` (`CommentDto`) |
| `CommentEdited` | `CommentEditedMessage` | `TaskId`, `CommentId`, `NewText`, `EditedAt?` |
| `CommentDeleted` | `CommentDeletedMessage` | `TaskId`, `CommentId` |

### Active User identity (token claims)

| Claim | Value |
|---|---|
| `sub` | The User's id, as a string. Read through the C# 14 extension property `ClaimsPrincipal.UserId`. |
| `iss`, `aud` | Fixed values from configuration (`Auth:Issuer`, `Auth:Audience`). |
| `exp` | Issue time plus 15 minutes, taken from `TimeProvider`. |

**Lifecycle**:

1. The token is issued after the landing-screen pick.
2. The web app holds it for the circuit, and renews it when less than 2 minutes remain.
3. It is checked on every request and on hub connect.
4. It is discarded with the circuit.

**Validation**: signature, issuer, audience and lifetime, with the default clock skew.

### Outcome model (`Taskify.Api`, server only)

| Type | Shape | Maps to |
|---|---|---|
| `Result<T>` | Success value, or an `Error` | `Ok`, `Created` or `NoContent` on success |
| `Error` | `Kind` (`ErrorKind`), `Detail` (string) | Problem Details with `detail` |
| `ErrorKind.NotFound` | Task, Project, Comment or User missing | 404 |
| `ErrorKind.Forbidden` | The caller is not the Comment's Author | 403 |
| `ErrorKind.Unprocessable` | Well-formed input that names a missing User | 422 |

Invalid input never reaches a service: validation returns 400 first. Missing or invalid identity never reaches a handler: authentication returns 401 first.

### Task-move metric

| Instrument | Type | Unit | Increment |
|---|---|---|---|
| `taskify.tasks.moved` on meter `Taskify.Api` | `Counter<long>` | `{move}` | +1 per successful Status change, after the save. A move to the same Status is not a change and does not count. |

## State transitions

The Task Status transitions are unchanged: any Status may move to any other, as before. A move to the same Status is rejected by the board before any request is sent, as today. The server still accepts it and broadcasts it; that behaviour is unchanged.
