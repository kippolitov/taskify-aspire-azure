using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Hubs;
using Taskify.Api.Observability;
using Taskify.Api.Results;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Shared.Realtime;
using Taskify.Shared.Requests;

namespace Taskify.Api.Services;

public class TaskService(
    TaskifyDbContext db,
    IHubContext<TaskifyHub, ITaskifyClient> hub,
    TimeProvider time,
    TaskifyMetrics metrics,
    ILogger<TaskService> logger
)
{
    /// <summary>
    /// Translated to SQL: the Comment count is a subquery, and no Comment row is read (FR-014).
    /// </summary>
    internal static readonly Expression<Func<TaskItem, TaskDto>> ToDto = t => new TaskDto(
        t.Id,
        t.ProjectId,
        t.Title,
        t.Description,
        t.Status,
        t.Assignee == null
            ? null
            : new UserDto(t.Assignee.Id, t.Assignee.DisplayName, t.Assignee.Role),
        t.CreatedAt,
        t.UpdatedAt,
        t.Comments.Count
    );

    internal static IQueryable<TaskDto> ProjectTasksQuery(TaskifyDbContext db, int projectId) =>
        db
            .TaskItems.AsNoTracking()
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Id)
            .Select(ToDto);

    public async Task<Result<IReadOnlyList<TaskDto>>> GetProjectTasksAsync(
        int projectId,
        CancellationToken ct
    )
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            return ProjectNotFound(projectId);
        }

        return Result<IReadOnlyList<TaskDto>>.Success(
            await ProjectTasksQuery(db, projectId).ToListAsync(ct)
        );
    }

    public async Task<Result<TaskDto>> GetTaskAsync(int id, CancellationToken ct)
    {
        var task = await TaskQuery(id).FirstOrDefaultAsync(ct);
        return task is null ? TaskNotFound(id) : task;
    }

    public async Task<Result<TaskDto>> CreateTaskAsync(
        int projectId,
        CreateTaskRequest request,
        CancellationToken ct
    )
    {
        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
        {
            return ProjectNotFound(projectId);
        }

        if (request.AssigneeId is { } assigneeId && !await UserExistsAsync(assigneeId, ct))
        {
            return UserNotFound(assigneeId);
        }

        var now = time.GetUtcNow();
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = request.Title,
            Description = request.Description,
            AssigneeId = request.AssigneeId,
            Status = ColumnStatus.ToDo,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync(ct);

        // Saved: announce it even if the caller has gone.
        var created = await ReadSavedAsync(task.Id);
        await BroadcastAsync(
            projectId,
            nameof(ITaskifyClient.TaskCreated),
            c => c.TaskCreated(created)
        );

        return created;
    }

    public async Task<Result<TaskDto>> UpdateTaskAsync(
        int id,
        UpdateTaskRequest request,
        CancellationToken ct
    )
    {
        var task = await db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        task.Title = request.Title;
        task.Description = request.Description;
        task.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return await ReadSavedAsync(id);
    }

    public async Task<Result<TaskDto>> AssignTaskAsync(
        int id,
        AssignTaskRequest request,
        CancellationToken ct
    )
    {
        var task = await db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        if (request.AssigneeId is { } assigneeId && !await UserExistsAsync(assigneeId, ct))
        {
            return UserNotFound(assigneeId);
        }

        var changed = task.AssigneeId != request.AssigneeId;
        task.AssigneeId = request.AssigneeId;
        task.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var dto = await ReadSavedAsync(id);
        if (changed)
        {
            await BroadcastAsync(
                dto.ProjectId,
                nameof(ITaskifyClient.TaskAssigned),
                c => c.TaskAssigned(new TaskAssignedMessage(dto.Id, dto.ProjectId, dto.Assignee))
            );
        }

        return dto;
    }

    public async Task<Result<TaskDto>> MoveTaskAsync(
        int id,
        ColumnStatus status,
        CancellationToken ct
    )
    {
        var task = await db.TaskItems.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (task is null)
        {
            return TaskNotFound(id);
        }

        var fromStatus = task.Status;
        task.Status = status;
        task.UpdatedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        if (fromStatus != status)
        {
            metrics.TaskMoved();
        }

        var dto = await ReadSavedAsync(id);
        await BroadcastAsync(
            dto.ProjectId,
            nameof(ITaskifyClient.TaskMoved),
            c =>
                c.TaskMoved(
                    new TaskMovedMessage(dto.Id, dto.ProjectId, fromStatus, status, dto.UpdatedAt)
                )
        );

        return dto;
    }

    /// <summary>
    /// Sends a live update after a saved change. A failure, such as Redis being down, is
    /// logged and counted; it never fails the change.
    /// </summary>
    private async Task BroadcastAsync(
        int projectId,
        string message,
        Func<ITaskifyClient, Task> send
    )
    {
        try
        {
            await send(hub.Clients.Group(Board(projectId)));
        }
        catch (Exception ex)
        {
            Log.BroadcastFailed(logger, message, projectId, ex);
            metrics.BroadcastFailed();
        }
    }

    private IQueryable<TaskDto> TaskQuery(int id) =>
        db.TaskItems.AsNoTracking().Where(t => t.Id == id).Select(ToDto);

    /// <summary>Reads a Task after its save; the save stands even if the caller cancels.</summary>
    private Task<TaskDto> ReadSavedAsync(int id) =>
        TaskQuery(id).SingleAsync(CancellationToken.None);

    private Task<bool> UserExistsAsync(int id, CancellationToken ct) =>
        db.Users.AnyAsync(u => u.Id == id, ct);

    private static string Board(int projectId) => $"board-{projectId}";

    private static Error TaskNotFound(int id) =>
        new(ErrorKind.NotFound, $"Task with id '{id}' was not found.");

    private static Error ProjectNotFound(int id) =>
        new(ErrorKind.NotFound, $"Project with id '{id}' was not found.");

    private static Error UserNotFound(int id) =>
        new(ErrorKind.Unprocessable, $"User with id '{id}' was not found.");
}
