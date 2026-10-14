using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Results;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/users")
            .WithTags("Users")
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        // Open: the landing screen lists the Users before anyone is picked (FR-025).
        group.MapGet("", GetAllAsync).AllowAnonymous();
        group
            .MapGet("/{id:int}", GetByIdAsync)
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<List<UserDto>>> GetAllAsync(
        TaskifyDbContext db,
        CancellationToken ct
    ) =>
        TypedResults.Ok(
            await db
                .Users.AsNoTracking()
                .OrderBy(u => u.Id)
                .Select(u => new UserDto(u.Id, u.DisplayName, u.Role))
                .ToListAsync(ct)
        );

    private static async Task<Results<Ok<UserDto>, ProblemHttpResult>> GetByIdAsync(
        int id,
        TaskifyDbContext db,
        CancellationToken ct
    )
    {
        var user = await db
            .Users.AsNoTracking()
            .Where(u => u.Id == id)
            .Select(u => new UserDto(u.Id, u.DisplayName, u.Role))
            .FirstOrDefaultAsync(ct);

        Result<UserDto> result = user is null
            ? new Error(ErrorKind.NotFound, $"User with id '{id}' was not found.")
            : user;
        return result.ToHttpResult();
    }
}
