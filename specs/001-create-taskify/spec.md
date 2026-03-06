# Feature Specification: Create Taskify

**Feature Branch**: `001-create-taskify`  
**Created**: March 5, 2026  
**Status**: Draft  
**Input**: User description: "Build Taskify, a team productivity platform with Kanban-style task boards. Five predefined users (one product manager, four engineers), three sample projects, standard Kanban columns (To Do, In Progress, In Review, Done), no login, task cards with unlimited comments, user assignment, drag-and-drop, and real-time board updates."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select User Identity (Priority: P1)

A visitor launches Taskify and is presented with a list of five predefined team members. They click their name and are immediately taken to the main application with their identity set. No password or credentials are required.

**Why this priority**: Without identity selection, no other feature is accessible. This is the single entry point to the entire application. Establishing "who I am" is fundamental to showing personal task highlights and enforcing comment ownership.

**Independent Test**: Can be fully tested by launching the app, verifying all five names and roles appear, clicking one, and confirming the application transitions to the project list view identifying the selected user.

**Acceptance Scenarios**:

1. **Given** the application is freshly launched, **When** the user views the landing screen, **Then** they see exactly five names — one labeled "Product Manager" and four labeled "Engineer"
2. **Given** the landing screen is displayed, **When** the user clicks a name, **Then** they are taken to the project list view with that user's name shown as the active user
3. **Given** the landing screen is displayed, **When** the user clicks a name, **Then** no password prompt or additional step is shown before entering the application

---

### User Story 2 - Browse Projects (Priority: P2)

After selecting their identity, the user lands on a dashboard showing all available projects. They can see project names at a glance and click any project to open its Kanban board.

**Why this priority**: The project list is the hub from which all productivity work flows. Without it, users cannot reach any Kanban board or work on any task.

**Independent Test**: Can be fully tested by selecting any user on the landing screen, confirming the project list shows exactly three projects, and clicking each to verify the Kanban board opens.

**Acceptance Scenarios**:

1. **Given** a user has been selected, **When** the main view loads, **Then** exactly three predefined sample projects are displayed
2. **Given** the project list is visible, **When** the user clicks a project, **Then** the Kanban board for that project opens
3. **Given** the project list is visible, **When** the user views the list, **Then** each project entry is distinguishable by name

---

### User Story 3 - View and Navigate the Kanban Board (Priority: P3)

The user opens a project and sees its Kanban board with four columns. Task cards are arranged in their respective columns. Cards assigned to the currently active user appear in a visually different color so they stand out at a glance.

**Why this priority**: The Kanban board is the core workspace. Delivering a clear, readable board is essential before adding interactivity.

**Independent Test**: Can be fully tested by opening a project and verifying four labeled columns appear, that sample task cards are distributed across columns, and that cards belonging to the active user are visually highlighted.

**Acceptance Scenarios**:

1. **Given** a project is opened, **When** the Kanban board renders, **Then** exactly four columns appear in order: "To Do," "In Progress," "In Review," "Done"
2. **Given** the Kanban board is displayed, **When** a task is assigned to the currently active user, **Then** that card uses a visually distinct color from cards assigned to others or unassigned
3. **Given** the Kanban board is displayed, **When** a task has no assigned user, **Then** it appears in a neutral/default color

---

### User Story 4 - Drag and Drop Tasks Between Columns (Priority: P4)

The user moves a task card from one column to another by dragging and dropping it. The card immediately appears in the target column. All other users viewing the same board see the move reflected without reloading the page.

**Why this priority**: Drag-and-drop status updates are the primary workflow interaction in a Kanban tool. Without this, the board is read-only and provides no practical productivity value.

**Independent Test**: Can be fully tested by dragging a card from one column to another, confirming it lands in the target column, and opening the same board in a second browser session to confirm the change appears there automatically.

**Acceptance Scenarios**:

1. **Given** the Kanban board is open, **When** the user drags a card to a different column and releases it, **Then** the card moves to the target column immediately
2. **Given** one user moves a card, **When** another user is viewing the same board simultaneously, **Then** the card appears in the new column on their screen without a page refresh
3. **Given** the user drags a card to the same column it already occupies, **When** the card is released, **Then** the column order remains unchanged and no error occurs

---

### User Story 5 - Assign a User to a Task (Priority: P5)

From any task card, the user can assign one of the five predefined team members as the responsible person. The assignment is visible on the card and immediately changes which cards are highlighted for the assigned user.

**Why this priority**: Task assignment provides ownership and accountability. It also directly drives the visual differentiation feature (highlighted cards), making it a prerequisite for that experience.

**Independent Test**: Can be fully tested by opening a task card, selecting a user from the assignment list, closing the card, and confirming the card now shows the assigned user's name and reflects the correct color if the assigned user matches the active identity.

**Acceptance Scenarios**:

1. **Given** a task card is open, **When** the user selects an assignee from the list, **Then** the card displays the assigned user's name
2. **Given** a task is assigned to User A, **When** User A is the active identity, **Then** that card is highlighted in the distinct "mine" color on the board
3. **Given** a task already has an assignee, **When** the user selects a different assignee, **Then** the previous assignment is replaced with the new one

---

### User Story 6 - Comment on a Task Card (Priority: P6)

The user opens a task card and adds a comment. They can add as many comments as needed. They can edit or delete their own comments but cannot modify comments left by others.

**Why this priority**: Comments enable team communication around a specific task. This is an additive collaboration feature that builds on top of the core board functionality.

**Independent Test**: Can be fully tested by opening a task, adding multiple comments, verifying edit and delete controls appear only on self-authored comments, editing one comment, deleting another, and confirming other users' comments show no modification controls.

**Acceptance Scenarios**:

1. **Given** a task card is open, **When** the user submits a comment, **Then** the comment appears in the card's comment thread attributed to the active user
2. **Given** a task card is open with multiple comments, **When** the user views the comment thread, **Then** their own comments have edit and delete controls; other users' comments do not
3. **Given** the user edits their comment and saves, **When** the comment thread is viewed, **Then** the updated text is shown
4. **Given** the user deletes their comment, **When** the comment thread is viewed, **Then** the comment is no longer present
5. **Given** a task card has no comments, **When** the user submits another comment, **Then** the comment count is not limited; any number of comments is accepted

---

### Edge Cases

- What happens when a task card has no assigned user and is displayed on the board?
- How does the board render when all tasks occupy a single column (e.g., all in "To Do")?
- What happens if a user drags a card and drops it back on its original column?
- When a comment is edited, is only the updated text shown, or is an "edited" indicator displayed alongside it?
- How are very long task titles or comment texts handled on a card to prevent layout breakage?
- What happens when two users attempt to move the same card simultaneously?

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Application MUST present a landing screen listing exactly five predefined users (one Product Manager, four Engineers) on every launch
- **FR-002**: Application MUST allow any user to be selected without a password or any other authentication step
- **FR-003**: After user selection, application MUST display the main project list view identifying the selected user as the active identity
- **FR-004**: Application MUST include exactly three predefined sample projects visible on the project list
- **FR-005**: Each project MUST have a Kanban board accessible by clicking on the project
- **FR-006**: Each Kanban board MUST display exactly four columns in a fixed order: "To Do," "In Progress," "In Review," "Done"
- **FR-007**: Each Kanban board MUST contain predefined sample task cards distributed across its columns at first launch
- **FR-008**: Task cards assigned to the currently active user MUST be displayed in a visually distinct color compared to all other cards
- **FR-009**: Users MUST be able to move task cards between columns using drag-and-drop interaction
- **FR-010**: Kanban board MUST reflect task card movements in real time for all users currently viewing the same board
- **FR-011**: Each task card MUST allow a user to assign exactly one of the five predefined users as its responsible person
- **FR-012**: Each task card MUST support an unlimited number of user comments
- **FR-013**: Users MUST be able to add a comment to any task card
- **FR-014**: Users MUST be able to edit any comment they authored
- **FR-015**: Users MUST be able to delete any comment they authored
- **FR-016**: Users MUST NOT be able to edit or delete comments authored by other users
- **FR-017**: Comments MUST be displayed in chronological order within a task card (oldest to newest)
- **FR-018**: Each comment MUST be attributed to the user who created it

### Key Entities

- **User**: Represents a predefined team member with a display name and a role (Product Manager or Engineer); exactly five exist in the system
- **Project**: Represents a unit of work containing a set of tasks; has a name; exactly three predefined projects exist at launch
- **Task**: Represents a unit of work within a project; has a title, a current column/status, an optional assignee (one User), and a list of comments
- **Column**: Represents a workflow stage on the Kanban board; one of four fixed values: "To Do," "In Progress," "In Review," "Done"
- **Comment**: Belongs to a Task; has text content, an author (User), a creation timestamp, and an optional last-edited timestamp

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Any of the five predefined users can launch the application and reach the project list in under 5 seconds from the landing screen
- **SC-002**: All three predefined sample projects and their respective Kanban boards are visible and navigable immediately upon entering the application with no setup steps
- **SC-003**: A task card can be moved from any column to any other column via drag-and-drop, with the change visible to the acting user within 1 second
- **SC-004**: A board change made by one user is reflected on another user's view of the same board within 3 seconds without a page reload
- **SC-005**: Task cards assigned to the active user are immediately distinguishable from other cards on the Kanban board without any additional navigation or filtering
- **SC-006**: An unlimited number of comments can be added to a single task card; no error is encountered after 50 or more comments
- **SC-007**: A user can add, edit, and delete their own comments; edit and delete controls are absent on other users' comments, verified across all five user identities

## Assumptions

- The five predefined users and three predefined sample projects are seeded automatically when the application is first set up; no manual data entry is required
- All five users are full members of all three projects with equal task interaction permissions in this initial phase
- The Product Manager role carries no special permissions beyond the role label in this initial phase
- Column ordering is fixed and cannot be customized by users in this phase
- There is no session persistence: if the page is refreshed, the user must re-select their identity from the landing screen
- Comments display the full text without a truncation limit in the card view
- An "edited" indicator is shown on comments that have been modified after initial submission

## Clarifications

### Session 2026-03-05

- Q: Target .NET version and C# language version for the implementation stack → A: .NET 10 / C# 14 (GA, released November 2025); all spec, plan, research, quickstart, and agent-context artifacts updated accordingly
