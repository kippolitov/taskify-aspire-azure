using System.ComponentModel.DataAnnotations;

namespace Taskify.Shared.Requests;

/// <summary>Sets the title and description. A <c>null</c> description clears it.</summary>
public record UpdateTaskRequest(
    [Required, StringLength(300, MinimumLength = 1)] string Title,
    string? Description
);
