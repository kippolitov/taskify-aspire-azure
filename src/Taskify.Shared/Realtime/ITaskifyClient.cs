using Taskify.Shared.Dtos;

namespace Taskify.Shared.Realtime;

/// <summary>
/// The messages the hub sends to board viewers. The server calls these through
/// <c>Hub&lt;ITaskifyClient&gt;</c>; clients register handlers with <c>nameof</c>.
/// </summary>
public interface ITaskifyClient
{
    Task TaskMoved(TaskMovedMessage message);

    Task TaskAssigned(TaskAssignedMessage message);

    Task TaskCreated(TaskDto task);

    Task CommentAdded(CommentAddedMessage message);

    Task CommentEdited(CommentEditedMessage message);

    Task CommentDeleted(CommentDeletedMessage message);
}
