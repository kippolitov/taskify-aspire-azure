using Taskify.Shared.Dtos;

namespace Taskify.Shared.Realtime;

public record CommentAddedMessage(int TaskId, CommentDto Comment);
