using Taskify.Shared.Enums;

namespace Taskify.Api.Data.Entities;

public class User
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }

    public ICollection<TaskItem> AssignedTasks { get; set; } = [];
    public ICollection<Comment> Comments { get; set; } = [];
}
