using Taskify.Shared.Enums;

namespace Taskify.Api.Data.Entities;

public class TaskItem
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public ColumnStatus Status { get; set; } = ColumnStatus.ToDo;
    public int? AssigneeId { get; set; }
    public User? Assignee { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Comment> Comments { get; set; } = [];
}
