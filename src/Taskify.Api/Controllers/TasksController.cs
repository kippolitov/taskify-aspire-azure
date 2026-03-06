using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Services;
using Taskify.Shared.Enums;

namespace Taskify.Api.Controllers;

[ApiController]
[Route("")]
public class TasksController(TaskifyDbContext db, TaskService taskService) : ControllerBase
{
    // GET /api/projects/{projectId}/tasks
    [HttpGet("api/projects/{projectId:int}/tasks")]
    public async Task<IActionResult> GetProjectTasks(int projectId)
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return Problem(
                detail: $"Project with id '{projectId}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var tasks = await taskService.GetProjectTasksAsync(projectId);
        return Ok(tasks);
    }

    // GET /api/tasks/{id}
    [HttpGet("api/tasks/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var task = await taskService.GetTaskAsync(id);
        if (task is null)
        {
            return Problem(
                detail: $"Task with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(task);
    }

    // POST /api/projects/{projectId}/tasks
    [HttpPost("api/projects/{projectId:int}/tasks")]
    public async Task<IActionResult> Create(int projectId, CreateTaskRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (
            request.AssigneeId.HasValue
            && !await db.Users.AnyAsync(u => u.Id == request.AssigneeId.Value)
        )
        {
            return Problem(
                detail: $"User with id '{request.AssigneeId}' was not found.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        var (dto, projectExists) = await taskService.CreateTaskAsync(
            projectId,
            request.Title,
            request.Description,
            request.AssigneeId
        );

        if (!projectExists)
        {
            return Problem(
                detail: $"Project with id '{projectId}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    // PUT /api/tasks/{id}
    [HttpPut("api/tasks/{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateTaskRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (
            request.AssigneeIdProvided
            && request.AssigneeId.HasValue
            && !await db.Users.AnyAsync(u => u.Id == request.AssigneeId.Value)
        )
        {
            return Problem(
                detail: $"User with id '{request.AssigneeId}' was not found.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        var dto = await taskService.UpdateTaskAsync(
            id,
            request.Title,
            request.Description,
            request.DescriptionProvided,
            request.AssigneeId,
            request.AssigneeIdProvided
        );

        if (dto is null)
        {
            return Problem(
                detail: $"Task with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(dto);
    }

    // PATCH /api/tasks/{id}/status
    [HttpPatch("api/tasks/{id:int}/status")]
    public async Task<IActionResult> MoveStatus(int id, MoveStatusRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var dto = await taskService.MoveTaskAsync(id, request.Status);
        if (dto is null)
        {
            return Problem(
                detail: $"Task with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(dto);
    }
}

public record CreateTaskRequest(
    [Required] [StringLength(300, MinimumLength = 1)] string Title,
    string? Description,
    int? AssigneeId
);

[System.Text.Json.Serialization.JsonConverter(typeof(UpdateTaskRequestConverter))]
public class UpdateTaskRequest
{
    [StringLength(300, MinimumLength = 1)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    // Null = remove assignee when AssigneeIdProvided is true
    public int? AssigneeId { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public bool AssigneeIdProvided { get; set; }

    // True when "description" key was present in the JSON body (even if null)
    [System.Text.Json.Serialization.JsonIgnore]
    public bool DescriptionProvided { get; set; }
}

public class UpdateTaskRequestConverter
    : System.Text.Json.Serialization.JsonConverter<UpdateTaskRequest>
{
    public override UpdateTaskRequest Read(
        ref System.Text.Json.Utf8JsonReader reader,
        Type typeToConvert,
        System.Text.Json.JsonSerializerOptions options
    )
    {
        using var doc = System.Text.Json.JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;

        string? title = root.TryGetProperty("title", out var tp) ? tp.GetString() : null;
        bool descriptionProvided = root.TryGetProperty("description", out var dp);
        string? description =
            descriptionProvided && dp.ValueKind != System.Text.Json.JsonValueKind.Null
                ? dp.GetString()
                : null;
        bool assigneeIdProvided = root.TryGetProperty("assigneeId", out var ap);
        int? assigneeId =
            assigneeIdProvided && ap.ValueKind != System.Text.Json.JsonValueKind.Null
                ? ap.GetInt32()
                : null;

        return new UpdateTaskRequest
        {
            Title = title,
            Description = description,
            AssigneeId = assigneeId,
            AssigneeIdProvided = assigneeIdProvided,
            DescriptionProvided = descriptionProvided,
        };
    }

    public override void Write(
        System.Text.Json.Utf8JsonWriter writer,
        UpdateTaskRequest value,
        System.Text.Json.JsonSerializerOptions options
    ) => throw new NotSupportedException();
}

public record MoveStatusRequest(
    [Required] [property: System.Text.Json.Serialization.JsonRequired] ColumnStatus Status
);
