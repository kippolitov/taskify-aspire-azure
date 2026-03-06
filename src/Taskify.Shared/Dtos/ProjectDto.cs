namespace Taskify.Shared.Dtos;

public record ProjectDto(int Id, string Name, string? Description, DateTimeOffset CreatedAt);
