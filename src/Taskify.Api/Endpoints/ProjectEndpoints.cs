using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Api.Results;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes
            .MapGroup("/api/projects")
            .WithTags("Projects")
            .RequireAuthorization()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet("", GetAllAsync);
        group.MapGet("/{id:int}", GetByIdAsync).ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static async Task<Ok<List<ProjectDto>>> GetAllAsync(
        TaskifyDbContext db,
        CancellationToken ct
    ) =>
        TypedResults.Ok(
            await db
                .Projects.AsNoTracking()
                .OrderBy(p => p.Id)
                .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt))
                .ToListAsync(ct)
        );

    private static async Task<Results<Ok<ProjectDto>, ProblemHttpResult>> GetByIdAsync(
        int id,
        TaskifyDbContext db,
        CancellationToken ct
    )
    {
        var project = await db
            .Projects.AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt))
            .FirstOrDefaultAsync(ct);

        Result<ProjectDto> result = project is null
            ? new Error(ErrorKind.NotFound, $"Project with id '{id}' was not found.")
            : project;
        return result.ToHttpResult();
    }
}
