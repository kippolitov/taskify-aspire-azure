# SignalR Hub Contract: Create Taskify

**Hub**: `TaskifyHub`  
**URL pattern**: `/hubs/taskify` (registered in `Taskify.Api`)  
**Transport**: WebSockets (fallback: Long Polling)  
**Version**: 1.0.0  
**Date**: March 5, 2026  
**Related**: [rest-api.md](rest-api.md) | [research.md](../research.md#r-003)

---

## Overview

`TaskifyHub` delivers real-time board events to all clients currently viewing the same project board. Clients subscribe to a **board group** on connection and receive push events whenever another user (or themselves via API calls from the server) mutates board state.

---

## Client → Server Methods

### `JoinBoard(int projectId)`

Called by the client immediately after connecting and navigating to a project board.  
Adds the connection to the group `board-{projectId}`.

**Parameters**:
- `projectId` — integer; the project whose board the client is viewing

**Response**: none (fire-and-forget)

**Error**: If `projectId` does not correspond to an existing project, the connection remains open but no group is joined; subsequent events for that project will not be delivered. *Client should validate `projectId` before calling.*

---

### `LeaveBoard(int projectId)`

Called when the client navigates away from a project board.  
Removes the connection from the group `board-{projectId}`.

**Parameters**:
- `projectId` — integer

**Response**: none

---

## Server → Client Events

The server broadcasts these events via `IHubContext<TaskifyHub>` from within API services. All payloads are serialized as camelCase JSON.

---

### `TaskMoved`

Fired when a task is moved to a different column.

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `PATCH /api/tasks/{id}/status`

**Payload**:
```json
{
  "taskId": 3,
  "projectId": 1,
  "fromStatus": "InProgress",
  "toStatus": "InReview",
  "movedAt": "2026-03-05T14:35:00Z"
}
```

**Client action**: Move the card with `taskId` from the `fromStatus` column to the `toStatus` column in the UI. If SortableJS has already optimistically moved the DOM, the Blazor diff will be a no-op.

---

### `TaskAssigned`

Fired when a task's assignee changes (including removal).

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `PUT /api/tasks/{id}` when `assigneeId` changes

**Payload**:
```json
{
  "taskId": 3,
  "projectId": 1,
  "assignee": {
    "id": 4,
    "displayName": "Marcus Johnson",
    "role": "Engineer"
  }
}
```
`assignee` is `null` when the assignee was removed.

**Client action**: Update the card's displayed assignee and re-evaluate the `card--mine` CSS class against the active user's ID.

---

### `TaskCreated`

Fired when a new task card is created in a project.

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `POST /api/projects/{projectId}/tasks`

**Payload**: full `TaskDto` (see [rest-api.md](rest-api.md#shared-types))
```json
{
  "id": 11,
  "projectId": 1,
  "title": "New task",
  "description": null,
  "status": "ToDo",
  "assignee": null,
  "createdAt": "2026-03-05T15:00:00Z",
  "updatedAt": "2026-03-05T15:00:00Z",
  "commentCount": 0
}
```

**Client action**: Insert a new card into the `ToDo` column.

---

### `CommentAdded`

Fired when a new comment is posted on a task.

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `POST /api/tasks/{taskId}/comments`

**Payload**:
```json
{
  "taskId": 3,
  "comment": {
    "id": 8,
    "taskId": 3,
    "author": { "id": 2, "displayName": "Alex Chen", "role": "Engineer" },
    "text": "Approved the design spec.",
    "createdAt": "2026-03-05T15:05:00Z",
    "editedAt": null
  }
}
```

**Client action**: If the detail view for `taskId` is open, append the comment to the thread and increment the visible comment count on the card.

---

### `CommentEdited`

Fired when a comment's text is updated.

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `PUT /api/comments/{id}`

**Payload**:
```json
{
  "taskId": 3,
  "commentId": 8,
  "newText": "Approved the design spec — revision 2.",
  "editedAt": "2026-03-05T15:10:00Z"
}
```

**Client action**: If the detail view for `taskId` is open, update the comment's text and show the `editedAt` timestamp.

---

### `CommentDeleted`

Fired when a comment is removed.

**Broadcast target**: group `board-{projectId}`  
**Trigger**: `DELETE /api/comments/{id}`

**Payload**:
```json
{
  "taskId": 3,
  "commentId": 8
}
```

**Client action**: If the detail view for `taskId` is open, remove the comment from the thread and decrement the visible comment count on the card.

---

## Connection Lifecycle

```
Client connects
    │
    ▼
Client calls JoinBoard(projectId)         ← must be called on KanbanBoard mount
    │
    ▼
Client receives push events               ← for the lifetime of the board view
    │
    ▼
Client calls LeaveBoard(projectId)        ← called on KanbanBoard dispose
    │
    ▼
Client disconnects (or navigates away)
```

If the client disconnects unexpectedly, SignalR automatically removes the connection from all groups. No explicit cleanup is required server-side.

---

## Error Handling

- If `JoinBoard` is called with an invalid `projectId`, the server logs a warning and returns silently.
- If the hub broadcasts an event and a client is no longer connected, SignalR discards the message silently (expected behavior for disconnected clients).
- The client (`BoardHubClient.cs`) implements reconnect logic via `HubConnectionBuilder.WithAutomaticReconnect()`. On reconnect, it re-calls `JoinBoard(projectId)` to re-subscribe to the current board's group.

---

## Serialization

- All JSON properties use **camelCase** (configured globally via `AddSignalR().AddJsonProtocol(opt => opt.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase)`).
- Enums are serialized as **strings** (`JsonStringEnumConverter`), not integers.
- `DateTimeOffset` values are serialized in ISO 8601 format (UTC, `Z` suffix).
