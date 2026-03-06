namespace Taskify.Shared.Dtos;

public record NotificationDto(
    int Id,
    int UserId,
    string Message,
    bool IsRead,
    DateTimeOffset CreatedAt
);
