# REST API Contract: Create Taskify

**Version**: 1.0.0  
**Date**: March 5, 2026  
**Base URL**: `http://localhost:{port}/api` (port assigned by Aspire; see Aspire dashboard)  
**Content-Type**: `application/json`  
**Auth**: None (Phase 1)

---

## Shared Types

### `UserDto`
```json
{
  "id": 1,
  "displayName": "Jordan Rivera",
  "role": "ProductManager"
}
```
`role` is one of: `"ProductManager"`, `"Engineer"`

### `ProjectDto`
```json
{
  "id": 1,
  "name": "Mobile Relaunch",
  "description": "Redesign and re-platform the mobile app experience",
  "createdAt": "2026-03-05T12:00:00Z"
}
```

### `TaskDto`
```json
{
  "id": 3,
  "projectId": 1,
  "title": "Auth flow redesign",
  "description": "Revamp the login and onboarding steps",
  "status": "InProgress",
  "assignee": { "id": 3, "displayName": "Priya Sharma", "role": "Engineer" },
  "createdAt": "2026-03-05T12:00:00Z",
  "updatedAt": "2026-03-05T14:30:00Z",
  "commentCount": 2
}
```
`status` is one of: `"ToDo"`, `"InProgress"`, `"InReview"`, `"Done"`  
`assignee` is `null` when unassigned.

### `CommentDto`
```json
{
  "id": 7,
  "taskId": 3,
  "author": { "id": 2, "displayName": "Alex Chen", "role": "Engineer" },
  "text": "Reviewed the Figma flow — looks good to me.",
  "createdAt": "2026-03-05T13:00:00Z",
  "editedAt": null
}
```
`editedAt` is `null` if the comment has never been edited.

### Error Response
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404,
  "detail": "Task with id '99' was not found."
}
```
All error responses follow RFC 9110 Problem Details (`application/problem+json`).

---

## Users

### `GET /api/users`

Returns all five predefined users.

**Response `200 OK`**
```json
[
  { "id": 1, "displayName": "Jordan Rivera", "role": "ProductManager" },
  { "id": 2, "displayName": "Alex Chen", "role": "Engineer" },
  { "id": 3, "displayName": "Priya Sharma", "role": "Engineer" },
  { "id": 4, "displayName": "Marcus Johnson", "role": "Engineer" },
  { "id": 5, "displayName": "Sofia Lindqvist", "role": "Engineer" }
]
```

### `GET /api/users/{id}`

Returns a single user by ID.

**Path parameters**:
- `id` — integer, required

**Response `200 OK`**: `UserDto`  
**Response `404 Not Found`**: Problem Details

---

## Projects

### `GET /api/projects`

Returns all projects.

**Response `200 OK`**: `ProjectDto[]`

### `GET /api/projects/{id}`

Returns a single project by ID.

**Path parameters**:
- `id` — integer, required

**Response `200 OK`**: `ProjectDto`  
**Response `404 Not Found`**: Problem Details

---

## Tasks

### `GET /api/projects/{projectId}/tasks`

Returns all tasks for the given project, ordered by `status` (column order) then `id`.

**Path parameters**:
- `projectId` — integer, required

**Response `200 OK`**: `TaskDto[]`  
**Response `404 Not Found`**: Problem Details (project not found)

### `GET /api/tasks/{id}`

Returns a single task by ID, including full details (no comments — use comments endpoint).

**Path parameters**:
- `id` — integer, required

**Response `200 OK`**: `TaskDto`  
**Response `404 Not Found`**: Problem Details

### `POST /api/projects/{projectId}/tasks`

Creates a new task in the given project.

**Path parameters**:
- `projectId` — integer, required

**Request body**:
```json
{
  "title": "New task title",
  "description": "Optional description",
  "assigneeId": 2
}
```
`description` and `assigneeId` are optional.

**Validation**:
- `title`: required, 1–300 characters
- `assigneeId`: must be a valid User ID when provided

**Response `201 Created`**: `TaskDto`  
**Location header**: `/api/tasks/{newId}`  
**Response `400 Bad Request`**: Problem Details (validation)  
**Response `404 Not Found`**: Problem Details (project not found)  
**Response `422 Unprocessable Entity`**: Problem Details (invalid assigneeId)

**Side effects**: Broadcasts `TaskCreated` event to board group `board-{projectId}` via SignalR (see [signalr-hub.md](signalr-hub.md)).

### `PUT /api/tasks/{id}`

Updates task title, description, and/or assignee. Partial update supported — omit fields to leave them unchanged.

**Path parameters**:
- `id` — integer, required

**Request body**:
```json
{
  "title": "Updated title",
  "description": "Updated description",
  "assigneeId": 3
}
```
Pass `"assigneeId": null` explicitly to remove the assignee.

**Validation**:
- `title`: 1–300 characters when provided
- `assigneeId`: valid User ID or `null`

**Response `200 OK`**: updated `TaskDto`  
**Response `400 Bad Request`**: Problem Details  
**Response `404 Not Found`**: Problem Details  
**Response `422 Unprocessable Entity`**: Problem Details

**Side effects**: If `assigneeId` changed, broadcasts `TaskAssigned` SignalR event.

### `PATCH /api/tasks/{id}/status`

Moves a task to a different column (status change only). Used by drag-and-drop.

**Path parameters**:
- `id` — integer, required

**Request body**:
```json
{
  "status": "InReview"
}
```

**Validation**:
- `status`: required, must be one of `"ToDo"`, `"InProgress"`, `"InReview"`, `"Done"`

**Response `200 OK`**: updated `TaskDto`  
**Response `400 Bad Request`**: Problem Details  
**Response `404 Not Found`**: Problem Details

**Side effects**: Broadcasts `TaskMoved` SignalR event to board group `board-{projectId}`.

---

## Comments

### `GET /api/tasks/{taskId}/comments`

Returns all comments for the given task, ordered by `createdAt` ascending (oldest first).

**Path parameters**:
- `taskId` — integer, required

**Response `200 OK`**: `CommentDto[]`  
**Response `404 Not Found`**: Problem Details

### `POST /api/tasks/{taskId}/comments`

Adds a new comment to the task.

**Path parameters**:
- `taskId` — integer, required

**Request body**:
```json
{
  "authorId": 2,
  "text": "Looks good to me!"
}
```

**Validation**:
- `authorId`: required, must be a valid User ID
- `text`: required, 1–10 000 characters

**Response `201 Created`**: `CommentDto`  
**Response `400 Bad Request`**: Problem Details  
**Response `404 Not Found`**: Problem Details (task not found)  
**Response `422 Unprocessable Entity`**: Problem Details (invalid authorId)

**Side effects**: Broadcasts `CommentAdded` SignalR event to board group `board-{projectId}`.

### `PUT /api/comments/{id}`

Edits the text of an existing comment. Only the comment's author may edit it.

**Path parameters**:
- `id` — integer, required

**Request body**:
```json
{
  "requestingUserId": 2,
  "text": "Updated comment text"
}
```

**Validation**:
- `requestingUserId`: required, must match `comment.AuthorId`
- `text`: required, 1–10 000 characters

**Response `200 OK`**: updated `CommentDto` (with `editedAt` set)  
**Response `400 Bad Request`**: Problem Details  
**Response `403 Forbidden`**: Problem Details (requestingUserId ≠ author)  
**Response `404 Not Found`**: Problem Details

**Side effects**: Broadcasts `CommentEdited` SignalR event.

### `DELETE /api/comments/{id}`

Deletes a comment. Only the comment's author may delete it.

**Path parameters**:
- `id` — integer, required

**Query parameters**:
- `requestingUserId` — integer, required

**Response `204 No Content`**  
**Response `403 Forbidden`**: Problem Details (requestingUserId ≠ author)  
**Response `404 Not Found`**: Problem Details

**Side effects**: Broadcasts `CommentDeleted` SignalR event.

---

## Notifications

### `GET /api/notifications`

Stub endpoint. Returns an empty array in Phase 1.  
*Retained to honour the declared API surface; business logic is deferred to Phase 2.*

**Response `200 OK`**
```json
[]
```

---

## HTTP Status Code Reference

| Code | Meaning |
|---|---|
| 200 | OK — successful read or update |
| 201 | Created — resource created |
| 204 | No Content — successful delete |
| 400 | Bad Request — invalid input format or missing required field |
| 403 | Forbidden — action not permitted for the requesting user |
| 404 | Not Found — resource does not exist |
| 422 | Unprocessable Entity — input is well-formed but semantically invalid |
