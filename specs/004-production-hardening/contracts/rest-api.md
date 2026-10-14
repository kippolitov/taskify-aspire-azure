# REST API Contract: Production Hardening

**Version**: 2.0.0 | **Date**: 2026-10-14
**Supersedes**: [specs/001-create-taskify/contracts/rest-api.md](../../001-create-taskify/contracts/rest-api.md) (1.0.0)
**Base URL**:
- Locally: the Aspire-assigned address of `taskify-api`.
- In production: the internal address of `taskify-api`, reachable only from `taskify-web` (FR-038).

**Content type**: `application/json`. Errors use `application/problem+json`.
**Description**: the OpenAPI 3.1 document at `/openapi/v1.json`, served in Development. It lists success and error shapes for every operation (FR-013).

## What changed from 1.0.0

| Area | 1.0.0 | 2.0.0 | Story |
|---|---|---|---|
| Authentication | None | Bearer JWT on every operation except `POST /api/auth/token` and `GET /api/users` | P3 |
| New operation | none | `POST /api/auth/token` | P3 |
| Task update | `PUT /api/tasks/{id}`, partial; a missing key means "unchanged"; also changed the Assignee | `PUT /api/tasks/{id}` sets `title` and `description` only; both are always sent | P2 |
| Assignee change | inside the `PUT` | `PATCH /api/tasks/{id}/assignee` | P2 |
| Add comment | body had `authorId` | body is `text` only; the Author comes from the token | P3 |
| Edit comment | body had `requestingUserId` | body is `text` only | P3 |
| Delete comment | `?requestingUserId=` query | no query; the caller comes from the token | P3 |
| Errors | Problem Details from hand-written code | Problem Details from the framework for 400, 401 and 500, and from one mapping for 403, 404 and 422 | P2 |
| 500 responses | unhandled | Problem Details with no exception detail | P2 |
| Cancellation | not honoured | every operation stops when the caller disconnects | P2 |

Request and response bodies are defined once in `Taskify.Shared` (FR-017). The shapes below are their JSON form: camelCase, enums as strings, and `DateTimeOffset` as ISO 8601 UTC.

## Shared types

`UserDto`, `ProjectDto`, `TaskDto` and `CommentDto` are unchanged from 1.0.0:

```json
{ "id": 1, "displayName": "Jordan Rivera", "role": "ProductManager" }
```

```json
{ "id": 3, "projectId": 1, "title": "Auth flow redesign", "description": "…", "status": "InProgress",
  "assignee": { "id": 3, "displayName": "Priya Sharma", "role": "Engineer" },
  "createdAt": "2026-03-05T12:00:00Z", "updatedAt": "2026-03-05T14:30:00Z", "commentCount": 2 }
```

- `status`: one of `ToDo`, `InProgress`, `InReview`, `Done`.
- `role`: one of `ProductManager`, `Engineer`.
- `assignee` and `editedAt` may be `null`.
- `commentCount` is computed in the database query; no Comment content is read (FR-014).

### Problem Details (every error)

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5", "title": "Not Found", "status": 404,
  "detail": "Task with id '99' was not found." }
```

A validation failure (400) adds `errors`, keyed by field:

```json
{ "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1", "title": "One or more validation errors occurred.",
  "status": 400, "errors": { "Title": ["The field Title must be a string with a minimum length of 1 and a maximum length of 300."] } }
```

A 500 carries `title` and `status` only, with no exception text or stack trace.

### Status codes

| Code | When |
|---|---|
| 200 | Read or update succeeded |
| 201 | Created (with a `Location` header) |
| 204 | Deleted |
| 400 | Input breaks a validation rule, or the body cannot be parsed |
| 401 | No valid bearer token, on an operation that requires one |
| 403 | The caller is not the Comment's Author |
| 404 | The Task, Project, Comment or User does not exist |
| 422 | Well-formed input names a User who does not exist |
| 500 | Unexpected failure |

## Identity

### `POST /api/auth/token`, anonymous

Issues an identity for the User picked on the landing screen (FR-021).

**Request**

```json
{ "userId": 2 }
```

**Responses**:
- `200`: `{ "accessToken": "<JWT>", "expiresAt": "2026-10-14T12:15:00Z" }`. The token lives 15 minutes.
- `400`: `userId` missing.
- `404`: no such User. No token is issued.

Clients send the token as `Authorization: Bearer <JWT>`, and renew it before `expiresAt` by calling this operation again.

## Users

### `GET /api/users`, anonymous

The five predefined Users, ordered by `id`. **Response** `200`: `UserDto[]`.

### `GET /api/users/{id}`

**Responses**: `200` `UserDto` · `401` · `404`.

## Projects

### `GET /api/projects`

**Responses**: `200` `ProjectDto[]`, ordered by `id` · `401`.

### `GET /api/projects/{id}`

**Responses**: `200` `ProjectDto` · `401` · `404`.

## Tasks

### `GET /api/projects/{projectId}/tasks`

Tasks of the Project, ordered by `status`, then `id`. **Responses**: `200` `TaskDto[]` · `401` · `404` (no such Project).

### `GET /api/tasks/{id}`

**Responses**: `200` `TaskDto` · `401` · `404`.

### `POST /api/projects/{projectId}/tasks`

**Request**

```json
{ "title": "New task", "description": "Optional", "assigneeId": 2 }
```

**Rules**:
- `title` is required, 1–300 characters.
- `description` and `assigneeId` are optional.
- `assigneeId`, when present, must name an existing User.

**Responses**: `201` `TaskDto`, with `Location: /api/tasks/{id}` · `400` · `401` · `404` (no such Project) · `422` (no such User).

**Broadcast**: `TaskCreated` to `board-{projectId}`.

### `PUT /api/tasks/{id}`

Sets the title and description. It does not touch the Assignee or Status (FR-015).

**Request**

```json
{ "title": "Updated title", "description": null }
```

**Rules**: `title` is required, 1–300 characters. `description` is required in the body but may be `null`, which clears it.

**Responses**: `200` `TaskDto` · `400` · `401` · `404`.

**Broadcast**: none, the same as 1.0.0 for a title or description change.

### `PATCH /api/tasks/{id}/assignee`

Sets or clears the Assignee. It touches nothing else.

**Request**

```json
{ "assigneeId": 3 }
```

Send `{ "assigneeId": null }` to clear the Assignee.

**Responses**: `200` `TaskDto` · `400` · `401` · `404` (no such Task) · `422` (no such User).

**Broadcast**: `TaskAssigned` to `board-{projectId}`, only when the Assignee actually changed.

### `PATCH /api/tasks/{id}/status`

**Request**

```json
{ "status": "InReview" }
```

**Rules**: `status` is required and must be one of the four Status names. Unknown names and undefined numbers both return 400.

**Responses**: `200` `TaskDto` · `400` · `401` · `404`.

**Effects**:
- Broadcasts `TaskMoved` to `board-{projectId}`.
- Increments `taskify.tasks.moved` when the Status actually changed.

## Comments

### `GET /api/tasks/{taskId}/comments`

Oldest first. **Responses**: `200` `CommentDto[]` · `401` · `404` (no such Task).

### `POST /api/tasks/{taskId}/comments`

**Request**

```json
{ "text": "Looks good to me!" }
```

**Rules**: `text` is required, 1–10,000 characters. The Author is the User named by the token (FR-026). Any other property in the body is ignored.

**Responses**: `201` `CommentDto`, with `Location: /api/tasks/{taskId}/comments/{id}` · `400` · `401` · `404` (no such Task).

**Broadcast**: `CommentAdded`.

### `PUT /api/comments/{id}`

**Request**

```json
{ "text": "Updated text" }
```

**Responses**: `200` `CommentDto`, with `editedAt` set · `400` · `401` · `403` (the caller is not the Author) · `404`.

**Broadcast**: `CommentEdited`.

### `DELETE /api/comments/{id}`

**Responses**: `204` · `401` · `403` (the caller is not the Author) · `404`.

**Broadcast**: `CommentDeleted`.

## Notifications

### `GET /api/notifications`

A placeholder that always returns `[]`. It is unchanged and out of scope. **Responses**: `200` `[]` · `401`.

## Cancellation and partial changes

Each operation that changes data performs a single save. A request cancelled before that save changes nothing and broadcasts nothing. Once the save has happened, the change stands and is broadcast even if the caller has gone (FR-016 and the spec's edge case).
