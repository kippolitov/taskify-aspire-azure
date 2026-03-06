using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(TaskifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await db
            .Projects.OrderBy(p => p.Id)
            .Select(p => new ProjectDto(p.Id, p.Name, p.Description, p.CreatedAt))
            .ToListAsync();

        return Ok(projects);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var project = await db.Projects.FindAsync(id);
        if (project is null)
        {
            return Problem(
                detail: $"Project with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(new ProjectDto(project.Id, project.Name, project.Description, project.CreatedAt));
    }
}
