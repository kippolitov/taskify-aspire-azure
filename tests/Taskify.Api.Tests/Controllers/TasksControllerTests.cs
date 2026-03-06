using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Taskify.Api.Controllers;
using Taskify.Api.Data;
using Taskify.Api.Services;
using Taskify.Api.Tests.Helpers;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Controllers;

/// <summary>T025a — TasksController integration-style unit tests.</summary>
public class TasksControllerTests
{
    private static (TasksController ctrl, TaskifyDbContext db) Build(string name)
    {
        var db = TestDbFactory.CreateWithSeed(name);
        var hub = new NullHubContext<Taskify.Api.Hubs.TaskifyHub>();
        var service = new TaskService(db, hub);
        var ctrl = new TasksController(db, service);
        return (ctrl, db);
    }

    [Fact]
    public async Task GetProjectTasks_ExistingProject_ReturnsOkWithTasks()
    {
        var (ctrl, _) = Build(nameof(GetProjectTasks_ExistingProject_ReturnsOkWithTasks));

        var result = await ctrl.GetProjectTasks(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var tasks = Assert.IsAssignableFrom<IEnumerable<TaskDto>>(ok.Value);
        Assert.Equal(2, tasks.Count());
    }

    [Fact]
    public async Task GetProjectTasks_NonExistingProject_Returns404()
    {
        var (ctrl, _) = Build(nameof(GetProjectTasks_NonExistingProject_Returns404));

        var result = await ctrl.GetProjectTasks(999);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task GetById_ExistingTask_ReturnsOkWithTask()
    {
        var (ctrl, _) = Build(nameof(GetById_ExistingTask_ReturnsOkWithTask));

        var result = await ctrl.GetById(1);

        var ok = Assert.IsType<OkObjectResult>(result);
        var task = Assert.IsType<TaskDto>(ok.Value);
        Assert.Equal(1, task.Id);
        Assert.Equal("Set up CI pipeline", task.Title);
    }

    [Fact]
    public async Task GetById_NonExistingTask_Returns404()
    {
        var (ctrl, _) = Build(nameof(GetById_NonExistingTask_Returns404));

        var result = await ctrl.GetById(999);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task MoveStatus_ExistingTask_ReturnsOkAndPersistsStatus()
    {
        var (ctrl, db) = Build(nameof(MoveStatus_ExistingTask_ReturnsOkAndPersistsStatus));
        var request = new MoveStatusRequest(ColumnStatus.Done);

        var result = await ctrl.MoveStatus(1, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var task = Assert.IsType<TaskDto>(ok.Value);
        Assert.Equal(ColumnStatus.Done, task.Status);

        // Verify persisted
        var persisted = await db.TaskItems.FindAsync(1);
        Assert.Equal(ColumnStatus.Done, persisted!.Status);
    }

    [Fact]
    public async Task MoveStatus_NonExistingTask_Returns404()
    {
        var (ctrl, _) = Build(nameof(MoveStatus_NonExistingTask_Returns404));
        var request = new MoveStatusRequest(ColumnStatus.Done);

        var result = await ctrl.MoveStatus(999, request);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedAndPersistsTask()
    {
        var (ctrl, db) = Build(nameof(Create_ValidRequest_ReturnsCreatedAndPersistsTask));
        var request = new CreateTaskRequest("New task", "Description", null);

        var result = await ctrl.Create(1, request);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var task = Assert.IsType<TaskDto>(created.Value);
        Assert.Equal("New task", task.Title);
        Assert.Equal(1, task.ProjectId);

        // Count tasks in project
        var count = db.TaskItems.Count(t => t.ProjectId == 1);
        Assert.Equal(3, count); // 2 seeded + 1 new
    }

    [Fact]
    public async Task Update_TitleOnly_UpdatesTitleAndLeavesDescriptionUnchanged()
    {
        var (ctrl, db) = Build(nameof(Update_TitleOnly_UpdatesTitleAndLeavesDescriptionUnchanged));

        // First set a description
        var task = await db.TaskItems.FindAsync(1);
        task!.Description = "Original description";
        await db.SaveChangesAsync();

        // PUT with only title change (no description key in body → DescriptionProvided=false)
        var request = new UpdateTaskRequest
        {
            Title = "Updated title",
            DescriptionProvided = false,
        };

        var result = await ctrl.Update(1, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<TaskDto>(ok.Value);
        Assert.Equal("Updated title", dto.Title);

        var persisted = await db.TaskItems.FindAsync(1);
        Assert.Equal("Original description", persisted!.Description);
    }
}
