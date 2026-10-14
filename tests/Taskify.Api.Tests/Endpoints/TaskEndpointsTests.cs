using System.Net;
using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Tests.Endpoints;

/// <summary>
/// Seeded Tasks: Project 1 holds Tasks 1–5, Project 2 holds 6–8, Project 3 holds 9–10.
/// Each test that changes data uses its own Task, so the order of tests does not matter.
/// </summary>
public class TaskEndpointsTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await fixture.CreateClientAsAsync(1);

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task GetProjectTasks_ExistingProject_ReturnsItsTasks_OrderedByStatusThenId()
    {
        var tasks = await _client.GetFromJsonAsync<List<TaskDto>>(
            "/api/projects/3/tasks",
            TestJson.Options
        );

        Assert.NotNull(tasks);
        Assert.Equal([10, 9], tasks.Select(t => t.Id));
        Assert.All(tasks, t => Assert.Equal(3, t.ProjectId));
    }

    [Fact]
    public async Task GetProjectTasks_NonExistingProject_Returns404()
    {
        var response = await _client.GetAsync("/api/projects/999/tasks");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetById_ExistingTask_ReturnsTaskWithAssignee()
    {
        var task = await _client.GetFromJsonAsync<TaskDto>("/api/tasks/3", TestJson.Options);

        Assert.NotNull(task);
        Assert.Equal("Auth flow redesign", task.Title);
        Assert.Equal(3, task.Assignee?.Id);
        Assert.Equal(0, task.CommentCount);
    }

    [Fact]
    public async Task GetById_NonExistingTask_Returns404()
    {
        var response = await _client.GetAsync("/api/tasks/999");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MoveStatus_ExistingTask_ReturnsTaskAndPersistsStatus()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/4/status",
            new { status = "Done" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Equal(ColumnStatus.Done, task!.Status);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(ColumnStatus.Done, (await db.TaskItems.FindAsync(4))!.Status);
    }

    [Fact]
    public async Task MoveStatus_NonExistingTask_Returns404()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/999/status",
            new { status = "Done" }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_Returns201WithLocation_AndPersistsTask()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects/2/tasks",
            new
            {
                title = "New task",
                description = "Description",
                assigneeId = 4,
            }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.NotNull(task);
        Assert.Equal("New task", task.Title);
        Assert.Equal(2, task.ProjectId);
        Assert.Equal(ColumnStatus.ToDo, task.Status);
        Assert.Equal(4, task.Assignee?.Id);
        Assert.Equal($"/api/tasks/{task.Id}", response.Headers.Location?.OriginalString);

        await using var db = fixture.CreateDbContext();
        var persisted = await db.TaskItems.FindAsync(task.Id);
        Assert.NotNull(persisted);
        Assert.Equal("Description", persisted.Description);
    }

    [Fact]
    public async Task Create_NonExistingProject_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects/999/tasks",
            new { title = "Orphan" }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_NonExistingAssignee_Returns422_AndCreatesNothing()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/projects/3/tasks",
            new { title = "Nobody's task", assigneeId = 999 }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        await using var db = fixture.CreateDbContext();
        Assert.DoesNotContain(db.TaskItems, t => t.Title == "Nobody's task");
    }

    [Fact]
    public async Task Update_SetsTitleAndDescription_AndLeavesAssigneeUnchanged()
    {
        await using (var db = fixture.CreateDbContext())
        {
            var seeded = await db.TaskItems.FindAsync(2);
            seeded!.Description = "Original description";
            await db.SaveChangesAsync();
        }

        var response = await _client.PutAsJsonAsync(
            "/api/tasks/2",
            new { title = "New", description = (string?)null }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Equal("New", task!.Title);
        Assert.Null(task.Description);

        await using var check = fixture.CreateDbContext();
        var persisted = await check.TaskItems.FindAsync(2);
        Assert.Equal("New", persisted!.Title);
        Assert.Null(persisted.Description);
        Assert.Equal(2, persisted.AssigneeId);
    }

    [Fact]
    public async Task Assign_SetsAssignee_LeavesTitle_AndBroadcastsTaskAssigned()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(2);
        var assigned = hub.WaitForAsync<TaskAssignedMessage>(
            nameof(ITaskifyClient.TaskAssigned),
            TimeSpan.FromSeconds(2)
        );

        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/6/assignee",
            new { assigneeId = 3 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Equal(3, task!.Assignee?.Id);
        Assert.Equal("Route configuration schema", task.Title);
        Assert.Equal(3, (await assigned).Assignee?.Id);

        await using var db = fixture.CreateDbContext();
        Assert.Equal(3, (await db.TaskItems.FindAsync(6))!.AssigneeId);
    }

    [Fact]
    public async Task Assign_Null_ClearsAssignee()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/7/assignee",
            new { assigneeId = (int?)null }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>(TestJson.Options);
        Assert.Null(task!.Assignee);

        await using var db = fixture.CreateDbContext();
        Assert.Null((await db.TaskItems.FindAsync(7))!.AssigneeId);
    }

    [Fact]
    public async Task Assign_NonExistingUser_Returns422_AndLeavesAssigneeUnchanged()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/8/assignee",
            new { assigneeId = 999 }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.UnprocessableEntity);
        await using var db = fixture.CreateDbContext();
        Assert.Null((await db.TaskItems.FindAsync(8))!.AssigneeId);
    }

    [Fact]
    public async Task Assign_NonExistingTask_Returns404()
    {
        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/999/assignee",
            new { assigneeId = 1 }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Assign_SameAssignee_DoesNotBroadcast()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(3);
        var assigned = hub.WaitForAsync<TaskAssignedMessage>(
            nameof(ITaskifyClient.TaskAssigned),
            TimeSpan.FromSeconds(1)
        );

        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/10/assignee",
            new { assigneeId = 3 }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await Assert.ThrowsAsync<TimeoutException>(() => assigned);
    }

    [Fact]
    public async Task Update_NonExistingTask_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/tasks/999",
            new { title = "x", description = (string?)null }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }
}
