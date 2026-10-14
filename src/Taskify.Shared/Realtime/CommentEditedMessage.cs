namespace Taskify.Shared.Realtime;

public record CommentEditedMessage(
    int TaskId,
    int CommentId,
    string NewText,
    DateTimeOffset? EditedAt
);
