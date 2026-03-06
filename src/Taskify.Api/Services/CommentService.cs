using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Data.Entities;
using Taskify.Api.Hubs;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Services;

public class CommentService(TaskifyDbContext db, IHubContext<TaskifyHub> hub)
{
    public async Task<List<CommentDto>> GetTaskCommentsAsync(int taskItemId)
    {
        return await db
            .Comments.Where(c => c.TaskItemId == taskItemId)
            .Include(c => c.Author)
            .OrderBy(c => c.CreatedAt)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<(CommentDto? dto, bool taskExists)> AddCommentAsync(
        int taskItemId,
        int authorId,
        string text
    )
    {
        var task = await db.TaskItems.AsNoTracking().FirstOrDefaultAsync(t => t.Id == taskItemId);

        if (task is null)
        {
            return (null, false);
        }

        var now = DateTimeOffset.UtcNow;
        var comment = new Comment
        {
            TaskItemId = taskItemId,
            AuthorId = authorId,
            Text = text,
            CreatedAt = now,
        };
        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        await db.Entry(comment).Reference(c => c.Author).LoadAsync();

        var dto = ToDto(comment);

        await hub
            .Clients.Group($"board-{task.ProjectId}")
            .SendAsync("CommentAdded", new { taskId = taskItemId, comment = dto });

        return (dto, true);
    }

    public async Task<(CommentDto? dto, bool forbidden)> EditCommentAsync(
        int id,
        int requestingUserId,
        string text
    )
    {
        var comment = await db
            .Comments.Include(c => c.Author)
            .Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment is null)
        {
            return (null, false);
        }

        if (comment.AuthorId != requestingUserId)
        {
            return (null, true);
        }

        comment.Text = text;
        comment.EditedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var dto = ToDto(comment);

        await hub
            .Clients.Group($"board-{comment.TaskItem.ProjectId}")
            .SendAsync(
                "CommentEdited",
                new
                {
                    taskId = comment.TaskItemId,
                    commentId = comment.Id,
                    newText = comment.Text,
                    editedAt = comment.EditedAt,
                }
            );

        return (dto, false);
    }

    public async Task<(bool found, bool forbidden)> DeleteCommentAsync(int id, int requestingUserId)
    {
        var comment = await db
            .Comments.Include(c => c.TaskItem)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (comment is null)
        {
            return (false, false);
        }

        if (comment.AuthorId != requestingUserId)
        {
            return (true, true);
        }

        var taskId = comment.TaskItemId;
        var projectId = comment.TaskItem.ProjectId;

        db.Comments.Remove(comment);
        await db.SaveChangesAsync();

        await hub
            .Clients.Group($"board-{projectId}")
            .SendAsync("CommentDeleted", new { taskId, commentId = id });

        return (true, false);
    }

    public static CommentDto ToDto(Comment c) =>
        new(
            c.Id,
            c.TaskItemId,
            new UserDto(c.Author.Id, c.Author.DisplayName, c.Author.Role),
            c.Text,
            c.CreatedAt,
            c.EditedAt
        );
}
