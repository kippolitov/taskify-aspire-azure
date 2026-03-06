using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Components.Shared;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T020a — TaskCard.razor bUnit tests.</summary>
public class TaskCardTests : BunitContext
{
    private static readonly TaskDto UnassignedTask = new(
        Id: 1,
        ProjectId: 1,
        Title: "Set up CI pipeline",
        Description: null,
        Status: ColumnStatus.ToDo,
        Assignee: null,
        CreatedAt: new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
        UpdatedAt: new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
        CommentCount: 0
    );

    private static readonly TaskDto AssignedTask = new(
        Id: 2,
        ProjectId: 1,
        Title: "Implement login",
        Description: "OAuth flow",
        Status: ColumnStatus.InProgress,
        Assignee: TestData.FiveUsers[1], // Bob Kim
        CreatedAt: new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
        UpdatedAt: new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
        CommentCount: 2
    );

    private void RegisterIdentity(UserDto? activeUser = null)
    {
        var identity = new IdentityService();
        if (activeUser is not null)
        {
            identity.SetUser(activeUser);
        }

        Services.AddScoped(_ => identity);
    }

    [Fact]
    public void TaskCard_RendersTaskTitle()
    {
        RegisterIdentity();

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, UnassignedTask));

        Assert.Contains("Set up CI pipeline", cut.Markup);
    }

    [Fact]
    public void TaskCard_HasDataTaskIdAttribute()
    {
        RegisterIdentity();

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, UnassignedTask));

        var card = cut.Find(".task-card");
        Assert.Equal("1", card.GetAttribute("data-task-id"));
    }

    [Fact]
    public void TaskCard_WhenTaskIsNotMine_DoesNotHaveMineClass()
    {
        RegisterIdentity(activeUser: TestData.FiveUsers[0]); // Alice, task assigned to Bob

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, AssignedTask));

        var card = cut.Find(".task-card");
        Assert.False(card.ClassList.Contains("task-card--mine"));
    }

    [Fact]
    public void TaskCard_WhenTaskIsAssignedToCurrentUser_HasMineClass()
    {
        RegisterIdentity(activeUser: TestData.FiveUsers[1]); // Bob Kim — same as task assignee

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, AssignedTask));

        var card = cut.Find(".task-card");
        Assert.True(card.ClassList.Contains("task-card--mine"));
    }

    [Fact]
    public void TaskCard_ShowsAssigneeName_WhenAssigned()
    {
        RegisterIdentity();

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, AssignedTask));

        Assert.Contains("Bob Kim", cut.Markup);
    }

    [Fact]
    public void TaskCard_ShowsUnassignedText_WhenNoAssignee()
    {
        RegisterIdentity();

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, UnassignedTask));

        Assert.Contains("Unassigned", cut.Markup);
    }

    [Fact]
    public void TaskCard_ShowsCommentCount()
    {
        RegisterIdentity();

        var cut = Render<TaskCard>(p => p.Add(c => c.Task, AssignedTask));

        Assert.Contains("2 comments", cut.Markup);
    }
}
