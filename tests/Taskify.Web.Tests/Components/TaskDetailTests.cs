using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Components.Shared;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T031a — TaskDetail renders assignee dropdown with all users.</summary>
public class TaskDetailTests : BunitContext
{
    private static TaskDto SampleTask(int? assigneeId = null) =>
        new(
            Id: 1,
            ProjectId: 1,
            Title: "Fix bug #42",
            Description: "Reproducible on iOS",
            Status: ColumnStatus.InProgress,
            Assignee: assigneeId.HasValue
                ? TestData.FiveUsers.Find(u => u.Id == assigneeId.Value)
                : null,
            CreatedAt: DateTimeOffset.UtcNow,
            UpdatedAt: DateTimeOffset.UtcNow,
            CommentCount: 0
        );

    private TestApiHandler _handler = null!;

    private IRenderedComponent<TaskDetail> Render(
        TaskDto? task = null,
        List<UserDto>? users = null,
        int? currentUserId = null
    )
    {
        var resolvedTask = task ?? SampleTask();
        var commentsJson = TestApiHandler.Serialize(new List<CommentDto>());
        var handler = _handler = new TestApiHandler(
            new Dictionary<string, string>
            {
                [$"/api/tasks/{resolvedTask.Id}/comments"] = commentsJson,
                [$"/api/tasks/{resolvedTask.Id}"] = TestApiHandler.Serialize(resolvedTask),
            }
        );
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        var identity = TestIdentity.Create();
        if (currentUserId.HasValue)
        {
            var user = TestData.FiveUsers.Find(u => u.Id == currentUserId.Value);
            if (user is not null)
            {
                TestIdentity.SignIn(identity, user);
            }
        }

        Services.AddSingleton(identity);
        Services.AddSingleton(new ApiClient(httpClient, identity));

        return Render<TaskDetail>(p =>
            p.Add(b => b.Task, resolvedTask)
                .Add(b => b.ProjectId, 1)
                .Add(b => b.AllUsers, users ?? TestData.FiveUsers)
        );
    }

    [Fact]
    public void TaskDetail_RendersTaskTitle()
    {
        var cut = Render();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));
        Assert.Contains("Fix bug #42", cut.Markup);
    }

    [Fact]
    public void TaskDetail_AssigneeDropdown_ContainsAllUsers()
    {
        var cut = Render();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));

        var options = cut.FindAll("select[aria-label='Assign task'] option");
        // 1 blank "Unassigned" option + 5 users = 6 total
        Assert.Equal(6, options.Count);
    }

    [Fact]
    public void TaskDetail_AssigneeDropdown_ListsAllUserNames()
    {
        var cut = Render();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));

        var selectMarkup = cut.Find("select[aria-label='Assign task']").TextContent;
        foreach (var user in TestData.FiveUsers)
        {
            Assert.Contains(user.DisplayName, selectMarkup);
        }
    }

    [Fact]
    public void TaskDetail_WhenAssigned_DropdownShowsCurrentAssignee()
    {
        var task = SampleTask(assigneeId: 2); // Bob Kim
        var cut = Render(task: task);
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));

        var selectedOption = cut.Find("select[aria-label='Assign task'] option[selected]");
        Assert.Contains("Bob Kim", selectedOption.TextContent);
    }

    [Fact]
    public void SaveEdit_SendsPut_WithOnlyTitleAndDescription()
    {
        var cut = Render(task: SampleTask(assigneeId: 2));
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));

        cut.FindAll("button").First(b => b.TextContent.Trim() == "Edit").Click();
        cut.Find("input[aria-label='Task title']").Change("Renamed");
        cut.FindAll("button").First(b => b.TextContent.Trim() == "Save").Click();

        var put = Assert.Single(_handler.Requests, r => r.Method == HttpMethod.Put);
        Assert.Equal("/api/tasks/1", put.Path);
        using var body = System.Text.Json.JsonDocument.Parse(put.Body!);
        Assert.Equal(
            ["description", "title"],
            body.RootElement.EnumerateObject().Select(p => p.Name).Order()
        );
        Assert.Equal("Renamed", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public void AssigneeChange_SendsPatchToAssignee()
    {
        var cut = Render();
        cut.WaitForState(() => !cut.Markup.Contains("spinner-border"), TimeSpan.FromSeconds(3));

        cut.Find("select[aria-label='Assign task']").Change("3");

        var patch = Assert.Single(_handler.Requests, r => r.Method == HttpMethod.Patch);
        Assert.Equal("/api/tasks/1/assignee", patch.Path);
        using var body = System.Text.Json.JsonDocument.Parse(patch.Body!);
        Assert.Equal(3, body.RootElement.GetProperty("assigneeId").GetInt32());
        Assert.DoesNotContain(_handler.Requests, r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public void TaskDetail_ClosesOnBackdropClick()
    {
        var closeCalled = false;
        var identity = TestIdentity.Create();
        Services.AddSingleton(identity);
        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
                ["/api/tasks/1/comments"] = TestApiHandler.Serialize(new List<CommentDto>()),
            }
        );
        Services.AddSingleton(
            new ApiClient(
                new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") },
                identity
            )
        );

        var cut = Render<TaskDetail>(p =>
            p.Add(b => b.Task, SampleTask())
                .Add(b => b.ProjectId, 1)
                .Add(b => b.AllUsers, TestData.FiveUsers)
                .Add(
                    b => b.OnClose,
                    Microsoft.AspNetCore.Components.EventCallback.Factory.Create(
                        this,
                        () => closeCalled = true
                    )
                )
        );

        cut.Find(".modal.d-block").Click();
        Assert.True(closeCalled);
    }
}
