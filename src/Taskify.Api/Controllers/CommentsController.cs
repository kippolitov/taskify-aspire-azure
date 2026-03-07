using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Services;

namespace Taskify.Api.Controllers;

[ApiController]
[Route("")]
public class CommentsController(TaskifyDbContext db, CommentService commentService) : ControllerBase
{
    // GET /api/tasks/{taskId}/comments
    [HttpGet("api/tasks/{taskId:int}/comments")]
    public async Task<IActionResult> GetTaskComments(int taskId)
    {
        var taskExists = await db.TaskItems.AnyAsync(t => t.Id == taskId);
        if (!taskExists)
        {
            return Problem(
                detail: $"Task with id '{taskId}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        var comments = await commentService.GetTaskCommentsAsync(taskId);
        return Ok(comments);
    }

    // POST /api/tasks/{taskId}/comments
    [HttpPost("api/tasks/{taskId:int}/comments")]
    public async Task<IActionResult> AddComment(int taskId, AddCommentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await db.Users.AnyAsync(u => u.Id == request.AuthorId))
        {
            return Problem(
                detail: $"User with id '{request.AuthorId}' was not found.",
                statusCode: StatusCodes.Status422UnprocessableEntity
            );
        }

        var (dto, taskExists) = await commentService.AddCommentAsync(
            taskId,
            request.AuthorId,
            request.Text
        );

        if (!taskExists)
        {
            return Problem(
                detail: $"Task with id '{taskId}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Created($"/api/tasks/{taskId}/comments/{dto!.Id}", dto);
    }

    // PUT /api/comments/{id}
    [HttpPut("api/comments/{id:int}")]
    public async Task<IActionResult> EditComment(int id, EditCommentRequest request)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var (dto, forbidden) = await commentService.EditCommentAsync(
            id,
            request.RequestingUserId,
            request.Text
        );

        if (forbidden)
        {
            return Problem(
                detail: "Only the comment's author may edit it.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        if (dto is null)
        {
            return Problem(
                detail: $"Comment with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(dto);
    }

    // DELETE /api/comments/{id}?requestingUserId=
    [HttpDelete("api/comments/{id:int}")]
    public async Task<IActionResult> DeleteComment(int id, [FromQuery] int requestingUserId)
    {
        var (found, forbidden) = await commentService.DeleteCommentAsync(id, requestingUserId);

        if (!found)
        {
            return Problem(
                detail: $"Comment with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        if (forbidden)
        {
            return Problem(
                detail: "Only the comment's author may delete it.",
                statusCode: StatusCodes.Status403Forbidden
            );
        }

        return NoContent();
    }
}

public record AddCommentRequest(
    [Required][property: JsonRequired] int AuthorId,
    [Required][StringLength(10000, MinimumLength = 1)] string Text
);

public record EditCommentRequest(
    [Required][property: JsonRequired] int RequestingUserId,
    [Required][StringLength(10000, MinimumLength = 1)] string Text
);
