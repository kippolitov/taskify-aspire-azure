using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController(TaskifyDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var users = await db
            .Users.OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.DisplayName, u.Role))
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
        {
            return Problem(
                detail: $"User with id '{id}' was not found.",
                statusCode: StatusCodes.Status404NotFound
            );
        }

        return Ok(new UserDto(user.Id, user.DisplayName, user.Role));
    }
}
