# SignalR Hub Contract: Production Hardening

**Version**: 2.0.0 | **Date**: 2026-10-14
**Supersedes**: [specs/001-create-taskify/contracts/signalr-hub.md](../../001-create-taskify/contracts/signalr-hub.md) (1.0.0)
**Hub**: `TaskifyHub : Hub<ITaskifyClient>`, mapped at `/hubs/taskify` in `Taskify.Api`.
**Contract source**: `Taskify.Shared/Realtime/`, which holds `ITaskifyClient` and one record per message (FR-018).

## What changed from 1.0.0

| Area | 1.0.0 | 2.0.0 | Story |
|---|---|---|---|
| Message definitions | Anonymous objects on the server, private records in the web app, string method names on both sides | One interface and one record per message, in `Taskify.Shared`; the server calls `Clients.Group(…).TaskMoved(message)`, and the client registers with `nameof(ITaskifyClient.TaskMoved)` | P2 |
| Status fields in C# | `string` on the client | `ColumnStatus` on both sides | P2 |
| Authentication | None | `[Authorize]`; a bearer token is required to connect | P3 |
| Transport from the web app | Default negotiation over the `https+http://` URL | WebSockets only, `SkipNegotiation = true`, to an address resolved through service discovery | P4 |
| Scale-out | One API instance | Any number of instances, joined by a Redis backplane with channel prefix `taskify` | P4 |

**Wire compatibility**: message names, field names, camelCase and enum-as-string are all unchanged. A 1.0.0 client and a 2.0.0 server exchange identical JSON. They differ only in that 2.0.0 requires a token to connect.

## Connecting

| Item | Value |
|---|---|
| URL | `{api-base}/hubs/taskify`. The web app resolves `{api-base}` with `ServiceEndpointResolver` for `https+http://taskify-api`. |
| Transport | WebSockets only, with `SkipNegotiation = true`. Integration tests may use long polling through `TestServer`. |
| Authentication | `Authorization: Bearer <JWT>` from `AccessTokenProvider`, which is called before every connect and reconnect, so an expired token is renewed first. |
| Without a valid token | The connection is refused with 401 (FR-028). |
| Token expiry while connected | The connection stays open until it disconnects, because `CloseOnAuthenticationExpiration` is off. A reconnect needs a valid token. |
| Reconnect | `WithAutomaticReconnect()`. On `Reconnected`, the client calls `JoinBoard` again, because group membership belongs to a connection. |

## Client → server methods

| Method | Parameters | Effect |
|---|---|---|
| `JoinBoard` | `int projectId` | Adds the connection to group `board-{projectId}`. Unchanged. |
| `LeaveBoard` | `int projectId` | Removes the connection from the group. Unchanged. |

Every User may view every Project. A User's Role is descriptive only (`CONTEXT.md`), so `JoinBoard` performs no further authorization.

## Server → client messages (`ITaskifyClient`)

All messages are sent to group `board-{projectId}` by the API services, after the change is saved. A broadcast that fails, for example while Redis is unavailable, is logged and counted. It never fails the saved change.

### `TaskMoved(TaskMovedMessage)`

Trigger: `PATCH /api/tasks/{id}/status`.

```json
{ "taskId": 3, "projectId": 1, "fromStatus": "InProgress", "toStatus": "InReview", "movedAt": "2026-10-14T14:35:00Z" }
```

### `TaskAssigned(TaskAssignedMessage)`

Trigger: `PATCH /api/tasks/{id}/assignee`, when the Assignee changed. In 1.0.0 this was `PUT /api/tasks/{id}`.

```json
{ "taskId": 3, "projectId": 1, "assignee": { "id": 4, "displayName": "Marcus Johnson", "role": "Engineer" } }
```

`assignee` is `null` when the Assignee was cleared.

### `TaskCreated(TaskDto)`

Trigger: `POST /api/projects/{projectId}/tasks`. The payload is the full `TaskDto`, unchanged.

### `CommentAdded(CommentAddedMessage)`

Trigger: `POST /api/tasks/{taskId}/comments`.

```json
{ "taskId": 3, "comment": { "id": 8, "taskId": 3, "author": { "id": 2, "displayName": "Alex Chen", "role": "Engineer" },
  "text": "Approved the design spec.", "createdAt": "2026-10-14T15:05:00Z", "editedAt": null } }
```

### `CommentEdited(CommentEditedMessage)`

Trigger: `PUT /api/comments/{id}`.

```json
{ "taskId": 3, "commentId": 8, "newText": "Approved the design spec — revision 2.", "editedAt": "2026-10-14T15:10:00Z" }
```

### `CommentDeleted(CommentDeletedMessage)`

Trigger: `DELETE /api/comments/{id}`.

```json
{ "taskId": 3, "commentId": 8 }
```

## Delivery across API instances

| Condition | Behaviour |
|---|---|
| Two or more API instances, Redis up | A message sent by any instance reaches every connection in the group, whichever instance holds it (FR-032). |
| Redis unavailable | The change is saved and the operation succeeds. Viewers on other instances may miss the message until Redis returns. SignalR does not buffer it. The failure is logged and visible in monitoring. |
| A client disconnected | The message is discarded, as in 1.0.0. |

## Serialization

One shared configuration in `Taskify.Shared`: camelCase properties, enums as strings and `DateTimeOffset` in ISO 8601 UTC (FR-020). It is applied to both the HTTP API and the hub's JSON protocol.
