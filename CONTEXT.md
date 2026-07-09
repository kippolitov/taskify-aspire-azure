# Taskify

A team productivity platform with Kanban-style task boards. Predefined users collaborate on projects by moving tasks across a shared board in real time.

## Language

**Task**:
A unit of work within a Project. Has a title, an optional description, a Status (workflow position), an optional Assignee, and a thread of Comments.
_Avoid_: TaskItem (implementation artifact), ticket, card, item, story

**Status**:
The current workflow position of a Task. One of four fixed values: To Do, In Progress, In Review, Done. Changing a task's Status is what "moving it across the board" means.
_Avoid_: column (UI rendering term), state, stage

**Project**:
A named container for a set of Tasks. Has a name and an optional description. The Kanban board view is how a Project's Tasks are displayed — "board" is UI vocabulary, not a separate domain concept.
_Avoid_: workspace, space, board (as a synonym for Project)

**User**:
A predefined team member with a display name and a Role. Exactly five exist in the system.
_Avoid_: team member, member, person

**Active User**:
The User who has selected themselves at the landing screen for the current browser session. Determines which task cards are highlighted and whose name appears on new comments. A client-side concept — the server has no session state.
_Avoid_: active identity, current user, logged-in user, authenticated user

**Role**:
The function a User plays on the team — either Product Manager or Engineer. Purely descriptive; does not govern access or permissions in the current system.
_Avoid_: permission, access level

**Comment**:
A piece of text attached to a Task, attributed to the User who wrote it. Can be edited or deleted only by its Author.
_Avoid_: note, message

**Author**:
The User who created a Comment. Used specifically in the context of Comments to mean the comment's originating User.
_Avoid_: creator, owner (in comment context)

**Notification**:
An in-app alert sent to a User when a Task they are the Assignee of changes Status, or when a new Comment is added to a Task they are assigned to. Has a read/unread state. Not yet implemented (Phase 2).
_Avoid_: alert, message, event
