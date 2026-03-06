using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Hubs;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;

namespace Taskify.Api.Services;

public class TaskService(TaskifyDbContext db, IHubContext<TaskifyHub> hub)
{
    public async Task<List<TaskDto>> GetProjectTasksAsync(int projectId)
    {
        return await db
            .TaskItems.Where(t => t.ProjectId == projectId)
            .Include(t => t.Assignee)
            .Include(t => t.Comments)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Id)
            .Select(t => ToDto(t))
            .ToListAsync();
    }

    public async Task<TaskDto?> GetTaskAsync(int id)
    {
        var task = await db
            .TaskItems.Include(t => t.Assignee)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        return task is null ? null : ToDto(task);
    }

    public async Task<(TaskDto dto, bool projectExists)> CreateTaskAsync(
        int projectId,
        string title,
        string? description,
        int? assigneeId
    )
    {
        var projectExists = await db.Projects.AnyAsync(p => p.Id == projectId);
        if (!projectExists)
        {
            return (null!, false);
        }

        var now = DateTimeOffset.UtcNow;
        var task = new TaskItem
        {
            ProjectId = projectId,
            Title = title,
            Description = description,
            AssigneeId = assigneeId,
            Status = ColumnStatus.ToDo,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.TaskItems.Add(task);
        await db.SaveChangesAsync();

        // reload with navigation
        var created = (await GetTaskAsync(task.Id))!;

        await hub.Clients.Group($"board-{projectId}").SendAsync("TaskCreated", created);

        return (created, true);
    }

    public async Task<TaskDto?> UpdateTaskAsync(
        int id,
        string? title,
        string? description,
        bool descriptionProvided,
        int? assigneeId,
        bool assigneeProvided
    )
    {
        var task = await db
            .TaskItems.Include(t => t.Assignee)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null)
        {
            return null;
        }

        var oldAssigneeId = task.AssigneeId;

        if (title is not null)
        {
            task.Title = title;
        }

        if (descriptionProvided)
        {
            task.Description = description;
        }

        if (assigneeProvided)
        {
            task.AssigneeId = assigneeId;
        }

        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        // reload to get assignee nav
        await db.Entry(task).Reference(t => t.Assignee).LoadAsync();

        var dto = ToDto(task);

        if (assigneeProvided && oldAssigneeId != assigneeId)
        {
            await hub
                .Clients.Group($"board-{task.ProjectId}")
                .SendAsync(
                    "TaskAssigned",
                    new
                    {
                        taskId = task.Id,
                        projectId = task.ProjectId,
                        assignee = task.Assignee is null
                            ? null
                            : new UserDto(
                                task.Assignee.Id,
                                task.Assignee.DisplayName,
                                task.Assignee.Role
                            ),
                    }
                );
        }

        return dto;
    }

    public async Task<TaskDto?> MoveTaskAsync(int id, ColumnStatus newStatus)
    {
        var task = await db
            .TaskItems.Include(t => t.Assignee)
            .Include(t => t.Comments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task is null)
        {
            return null;
        }

        var fromStatus = task.Status;
        task.Status = newStatus;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var dto = ToDto(task);

        await hub
            .Clients.Group($"board-{task.ProjectId}")
            .SendAsync(
                "TaskMoved",
                new
                {
                    taskId = task.Id,
                    projectId = task.ProjectId,
                    fromStatus,
                    toStatus = newStatus,
                    movedAt = task.UpdatedAt,
                }
            );

        return dto;
    }

    public static TaskDto ToDto(TaskItem t) =>
        new(
            t.Id,
            t.ProjectId,
            t.Title,
            t.Description,
            t.Status,
            t.Assignee is null
                ? null
                : new UserDto(t.Assignee.Id, t.Assignee.DisplayName, t.Assignee.Role),
            t.CreatedAt,
            t.UpdatedAt,
            t.Comments?.Count ?? 0
        );
}
