using Taskify.Api.Data.Entities;
using Taskify.Api.Services;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Services;

public class TaskServiceToDtoTests
{
    [Fact]
    public void ToDto_WithNoAssigneeAndNoComments_MapsAllFieldsCorrectly()
    {
        var now = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);
        var task = new TaskItem
        {
            Id = 1,
            ProjectId = 2,
            Title = "Fix login bug",
            Description = "Null ref on empty email",
            Status = ColumnStatus.InProgress,
            CreatedAt = now,
            UpdatedAt = now,
            Comments = [],
        };

        var dto = TaskService.ToDto(task);

        Assert.Equal(1, dto.Id);
        Assert.Equal(2, dto.ProjectId);
        Assert.Equal("Fix login bug", dto.Title);
        Assert.Equal("Null ref on empty email", dto.Description);
        Assert.Equal(ColumnStatus.InProgress, dto.Status);
        Assert.Null(dto.Assignee);
        Assert.Equal(now, dto.CreatedAt);
        Assert.Equal(now, dto.UpdatedAt);
        Assert.Equal(0, dto.CommentCount);
    }

    [Fact]
    public void ToDto_WithAssignee_MapsAssigneeToUserDto()
    {
        var now = DateTimeOffset.UtcNow;
        var assignee = new User
        {
            Id = 5,
            DisplayName = "Alice",
            Role = UserRole.Engineer,
        };
        var task = new TaskItem
        {
            Id = 3,
            ProjectId = 1,
            Title = "Design new UI",
            Status = ColumnStatus.ToDo,
            CreatedAt = now,
            UpdatedAt = now,
            Assignee = assignee,
            Comments = [],
        };

        var dto = TaskService.ToDto(task);

        Assert.NotNull(dto.Assignee);
        Assert.Equal(5, dto.Assignee.Id);
        Assert.Equal("Alice", dto.Assignee.DisplayName);
        Assert.Equal(UserRole.Engineer, dto.Assignee.Role);
    }

    [Fact]
    public void ToDto_WithComments_ReflectsCommentCount()
    {
        var now = DateTimeOffset.UtcNow;
        var task = new TaskItem
        {
            Id = 7,
            ProjectId = 2,
            Title = "Add logging",
            Status = ColumnStatus.Done,
            CreatedAt = now,
            UpdatedAt = now,
            Comments =
            [
                new Comment
                {
                    Id = 1,
                    TaskItemId = 7,
                    AuthorId = 1,
                    Text = "Looks good",
                    CreatedAt = now,
                },
                new Comment
                {
                    Id = 2,
                    TaskItemId = 7,
                    AuthorId = 2,
                    Text = "LGTM",
                    CreatedAt = now,
                },
            ],
        };

        var dto = TaskService.ToDto(task);

        Assert.Equal(2, dto.CommentCount);
    }
}
