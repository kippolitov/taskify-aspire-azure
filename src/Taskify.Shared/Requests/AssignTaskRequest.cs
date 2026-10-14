namespace Taskify.Shared.Requests;

/// <summary>
/// <c>null</c> clears the Assignee. A value must name an existing User; the service returns 422 otherwise.
/// </summary>
public record AssignTaskRequest(int? AssigneeId);
