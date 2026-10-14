using System.Linq.Expressions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Hubs;
using Taskify.Api.Observability;
using Taskify.Api.Results;
using Taskify.Shared.Dtos;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Services;

public class CommentService(
    TaskifyDbContext db,
    IHubContext<TaskifyHub, ITaskifyClient> hub,
    TimeProvider time,
    TaskifyMetrics metrics,
    ILogger<CommentService> logger
)
{
    private static readonly Expression<Func<Comment, CommentDto>> ToDto = c => new CommentDto(
        c.Id,
        c.TaskItemId,
        new UserDto(c.Author.Id, c.Author.DisplayName, c.Author.Role),
        c.Text,
        c.CreatedAt,
        c.EditedAt
    );

    public async Task<Result<IReadOnlyList<CommentDto>>> GetTaskCommentsAsync(
        int taskId,
        CancellationToken ct
    )
    {
        if (!await db.TaskItems.AnyAsync(t => t.Id == taskId, ct))
        {
            return TaskNotFound(taskId);
        }

        return Result<IReadOnlyList<CommentDto>>.Success(
            await db
                .Comments.AsNoTracking()
                .Where(c => c.TaskItemId == taskId)
                .OrderBy(c => c.CreatedAt)
                .ThenBy(c => c.Id)
                .Select(ToDto)
                .ToListAsync(ct)
        );
    }

    public async Task<Result<CommentDto>> AddCommentAsync(
        int taskId,
        int callerId,
        string text,
        CancellationToken ct
    )
    {
        var projectId = await db
            .TaskItems.Where(t => t.Id == taskId)
            .Select(t => (int?)t.ProjectId)
            .FirstOrDefaultAsync(ct);
        if (projectId is null)
        {
            return TaskNotFound(taskId);
        }

        var comment = new Comment
        {
            TaskItemId = taskId,
            AuthorId = callerId,
            Text = text,
            CreatedAt = time.GetUtcNow(),
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync(ct);

        // Saved: announce it even if the caller has gone.
        var dto = await ReadSavedAsync(comment.Id);
        await BroadcastAsync(
            projectId.Value,
            nameof(ITaskifyClient.CommentAdded),
            c => c.CommentAdded(new CommentAddedMessage(taskId, dto))
        );

        return dto;
    }

    public async Task<Result<CommentDto>> EditCommentAsync(
        int id,
        int callerId,
        string text,
        CancellationToken ct
    )
    {
        var comment = await db
            .Comments.Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment is null)
        {
            return CommentNotFound(id);
        }

        if (comment.AuthorId != callerId)
        {
            return new Error(ErrorKind.Forbidden, "Only the comment's author may edit it.");
        }

        comment.Text = text;
        comment.EditedAt = time.GetUtcNow();
        await db.SaveChangesAsync(ct);

        var dto = await ReadSavedAsync(id);
        await BroadcastAsync(
            comment.TaskItem.ProjectId,
            nameof(ITaskifyClient.CommentEdited),
            c =>
                c.CommentEdited(
                    new CommentEditedMessage(comment.TaskItemId, id, dto.Text, dto.EditedAt)
                )
        );

        return dto;
    }

    public async Task<Result> DeleteCommentAsync(int id, int callerId, CancellationToken ct)
    {
        var comment = await db
            .Comments.Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (comment is null)
        {
            return CommentNotFound(id);
        }

        if (comment.AuthorId != callerId)
        {
            return new Error(ErrorKind.Forbidden, "Only the comment's author may delete it.");
        }

        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);

        await BroadcastAsync(
            comment.TaskItem.ProjectId,
            nameof(ITaskifyClient.CommentDeleted),
            c => c.CommentDeleted(new CommentDeletedMessage(comment.TaskItemId, id))
        );

        return Result.Success;
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

    /// <summary>Reads a Comment after its save; the save stands even if the caller cancels.</summary>
    private Task<CommentDto> ReadSavedAsync(int id) =>
        db
            .Comments.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToDto)
            .SingleAsync(CancellationToken.None);

    private static string Board(int projectId) => $"board-{projectId}";

    private static Error TaskNotFound(int id) =>
        new(ErrorKind.NotFound, $"Task with id '{id}' was not found.");

    private static Error CommentNotFound(int id) =>
        new(ErrorKind.NotFound, $"Comment with id '{id}' was not found.");
}
