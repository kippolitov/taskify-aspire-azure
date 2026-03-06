using Taskify.Shared.Enums;

namespace Taskify.Shared.Dtos;

public record TaskDto(
    int Id,
    int ProjectId,
    string Title,
    string? Description,
    ColumnStatus Status,
    UserDto? Assignee,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    int CommentCount
);
