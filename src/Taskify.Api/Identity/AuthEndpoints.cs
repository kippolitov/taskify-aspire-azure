using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Results;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;

namespace Taskify.Api.Identity;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/auth")
            .WithTags("Identity")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group
            .MapPost("/token", IssueAsync)
            .AllowAnonymous()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Results<Ok<TokenResponse>, ProblemHttpResult>> IssueAsync(
        TokenRequest request,
        TaskifyDbContext db,
        TokenIssuer issuer,
        CancellationToken ct
    )
    {
        Result<TokenResponse> result = await db.Users.AnyAsync(u => u.Id == request.UserId, ct)
            ? issuer.Issue(request.UserId)
            : new Error(ErrorKind.NotFound, $"User with id '{request.UserId}' was not found.");
        return result.ToHttpResult();
    }
}
