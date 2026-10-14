namespace Taskify.Shared.Realtime;

public record CommentDeletedMessage(int TaskId, int CommentId);
