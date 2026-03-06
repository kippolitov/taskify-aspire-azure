using Microsoft.AspNetCore.Mvc;

namespace Taskify.Api.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    /// <summary>
    /// Stub endpoint — returns an empty array in Phase 1.
    /// Business logic deferred to Phase 2.
    /// </summary>
    [HttpGet]
    public IActionResult GetAll() => Ok(Array.Empty<object>());
}
