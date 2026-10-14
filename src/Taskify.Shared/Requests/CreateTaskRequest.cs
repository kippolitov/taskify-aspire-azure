using System.ComponentModel.DataAnnotations;

namespace Taskify.Shared.Requests;

/// <summary>
/// <c>AssigneeId</c>, when present, must name an existing User; the service returns 422 otherwise.
/// </summary>
public record CreateTaskRequest(
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    string? Description,
    int? AssigneeId
);
