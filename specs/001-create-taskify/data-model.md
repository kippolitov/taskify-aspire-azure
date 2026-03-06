# Data Model: Create Taskify

**Phase**: 1 — Design & Contracts  
**Date**: March 5, 2026  
**Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

---

## Entity Overview

```
User ──< TaskItem (assignee, nullable)
User ──< Comment (author)
Project ──< TaskItem
TaskItem ──< Comment
```

---

## Entities

### User

Represents a predefined team member. Exactly five exist; created by the seed only.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `int` | PK, identity | Auto-generated |
| `DisplayName` | `string` | NOT NULL, max 100, unique | e.g. "Alex Chen" |
| `Role` | `UserRole` (enum) | NOT NULL | `ProductManager` or `Engineer` |

**Seed data** (five predefined users):

| Id | DisplayName | Role |
|---|---|---|
| 1 | Jordan Rivera | ProductManager |
| 2 | Alex Chen | Engineer |
| 3 | Priya Sharma | Engineer |
| 4 | Marcus Johnson | Engineer |
| 5 | Sofia Lindqvist | Engineer |

**Validation rules**:
- `DisplayName` must be non-empty and ≤ 100 characters.
- `Role` must be a valid `UserRole` enum value.

---

### Project

Represents a unit of work containing tasks. Exactly three seeded at launch.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `int` | PK, identity | |
| `Name` | `string` | NOT NULL, max 200, unique | e.g. "Mobile Relaunch" |
| `Description` | `string` | nullable, max 1 000 | Optional summary |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | Set at insert |

**Seed data** (three predefined projects):

| Id | Name | Description |
|---|---|---|
| 1 | Mobile Relaunch | Redesign and re-platform the mobile app experience |
| 2 | API Gateway v2 | Build the next-generation internal API gateway |
| 3 | Design System | Establish shared UI component library and tokens |

---

### TaskItem

Represents a unit of work within a project. Cards on the Kanban board.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `int` | PK, identity | |
| `ProjectId` | `int` | FK → `Project.Id`, NOT NULL | Owning project |
| `Title` | `string` | NOT NULL, max 300 | Displayed on the card |
| `Description` | `string` | nullable, max 4 000 | Shown in detail view |
| `Status` | `ColumnStatus` (enum) | NOT NULL, default `ToDo` | Current Kanban column |
| `AssigneeId` | `int?` | FK → `User.Id`, nullable | One or zero assignee |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | Set at insert |
| `UpdatedAt` | `DateTimeOffset` | NOT NULL | Updated on any mutation |

**ColumnStatus enum**:

| Value | Ordinal | Display Label |
|---|---|---|
| `ToDo` | 0 | To Do |
| `InProgress` | 1 | In Progress |
| `InReview` | 2 | In Review |
| `Done` | 3 | Done |

**Validation rules**:
- `Title` must be non-empty and ≤ 300 characters.
- `Status` must be a valid `ColumnStatus` value.
- `AssigneeId` must reference an existing `User.Id` when provided.
- `ProjectId` must reference an existing `Project.Id`.

**Seed data**: 10 tasks spread across three projects, distributed across columns to give a representative initial board.

| Id | ProjectId | Title | Status | AssigneeId |
|---|---|---|---|---|
| 1 | 1 | Define new navigation structure | Done | 1 |
| 2 | 1 | Implement bottom tab bar | InReview | 2 |
| 3 | 1 | Auth flow redesign | InProgress | 3 |
| 4 | 1 | Accessibility audit | InProgress | 4 |
| 5 | 1 | Beta release preparation | ToDo | null |
| 6 | 2 | Route configuration schema | InReview | 2 |
| 7 | 2 | Rate limiting middleware | InProgress | 5 |
| 8 | 2 | Load test report | ToDo | null |
| 9 | 3 | Color token definition | Done | 1 |
| 10 | 3 | Button component | InProgress | 3 |

---

### Comment

A message attached to a `TaskItem` by a `User`.

| Field | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | `int` | PK, identity | |
| `TaskItemId` | `int` | FK → `TaskItem.Id`, NOT NULL | Owning task |
| `AuthorId` | `int` | FK → `User.Id`, NOT NULL | Who wrote it |
| `Text` | `string` | NOT NULL, max 10 000 | Comment body |
| `CreatedAt` | `DateTimeOffset` | NOT NULL | Set at insert |
| `EditedAt` | `DateTimeOffset?` | nullable | Set on edit; null if never edited |

**Validation rules**:
- `Text` must be non-empty and ≤ 10 000 characters.
- `AuthorId` must reference an existing `User.Id`.
- `TaskItemId` must reference an existing `TaskItem.Id`.

**Business rules**:
- A user may only edit or delete comments where `AuthorId` matches their own `User.Id`.
- `EditedAt` must be set to the current UTC timestamp when `Text` is modified.
- Comments are ordered ascending by `CreatedAt` in all list responses.
- No upper limit on the number of comments per task.

---

## Enums Reference

### `UserRole`
```csharp
public enum UserRole
{
    ProductManager = 0,
    Engineer = 1,
}
```

### `ColumnStatus`
```csharp
public enum ColumnStatus
{
    ToDo = 0,
    InProgress = 1,
    InReview = 2,
    Done = 3,
}
```

---

## State Transitions

```
TaskItem.Status transitions (any direction is permitted — no guards in Phase 1):

ToDo ⟷ InProgress ⟷ InReview ⟷ Done
 └──────────────────────────────────┘
              (direct jumps allowed)
```

A drag-and-drop move from any column to any other column is valid. The API enforces only that the target `ColumnStatus` value is a valid enum member.

---

## EF Core Notes

- **Table naming**: snake_case via `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions package).
- **Cascade deletes**: `TaskItem` delete cascades to `Comment`. `Project` delete cascades to `TaskItem` (and thus to `Comment`). `User` delete is restricted — cannot delete a user who has authored comments or is assigned to tasks (enforced by FK constraint and validated in service layer).
- **Concurrency**: Optimistic concurrency not required in Phase 1 (five users, low contention). `UpdatedAt` serves as a soft indicator for last mutation.
- **Indexes**: `TaskItem(ProjectId)`, `TaskItem(AssigneeId)`, `Comment(TaskItemId)`, `Comment(AuthorId)`.
