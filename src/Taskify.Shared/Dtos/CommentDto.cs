namespace Taskify.Shared.Dtos;

public record CommentDto(
    int Id,
    int TaskId,
    UserDto Author,
    string Text,
    DateTimeOffset CreatedAt,
    DateTimeOffset? EditedAt
);
