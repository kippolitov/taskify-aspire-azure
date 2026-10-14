using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Taskify.Api.Identity;
using Taskify.Api.Results;
using Taskify.Api.Services;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;

namespace Taskify.Api.Endpoints;

public static class CommentEndpoints
{
    public static RouteGroupBuilder MapCommentEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api")
            .WithTags("Comments")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group
            .MapGet("/tasks/{taskId:int}/comments", GetTaskCommentsAsync)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group
            .MapPost("/tasks/{taskId:int}/comments", AddAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        group
            .MapPut("/comments/{id:int}", EditAsync)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);
        group
            .MapDelete("/comments/{id:int}", DeleteAsync)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<
        Results<Ok<IReadOnlyList<CommentDto>>, ProblemHttpResult>
    > GetTaskCommentsAsync(int taskId, CommentService comments, CancellationToken ct) =>
        (await comments.GetTaskCommentsAsync(taskId, ct)).ToHttpResult();

    private static async Task<Results<Created<CommentDto>, ProblemHttpResult>> AddAsync(
        int taskId,
        AddCommentRequest request,
        ClaimsPrincipal user,
        CommentService comments,
        CancellationToken ct
    ) =>
        (await comments.AddCommentAsync(taskId, user.UserId, request.Text, ct)).ToCreatedResult(c =>
            $"/api/tasks/{taskId}/comments/{c.Id}"
        );

    private static async Task<Results<Ok<CommentDto>, ProblemHttpResult>> EditAsync(
        int id,
        EditCommentRequest request,
        ClaimsPrincipal user,
        CommentService comments,
        CancellationToken ct
    ) => (await comments.EditCommentAsync(id, user.UserId, request.Text, ct)).ToHttpResult();

    private static async Task<Results<NoContent, ProblemHttpResult>> DeleteAsync(
        int id,
        ClaimsPrincipal user,
        CommentService comments,
        CancellationToken ct
    ) => (await comments.DeleteCommentAsync(id, user.UserId, ct)).ToNoContentResult();
}
