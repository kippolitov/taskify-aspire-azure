using Taskify.Shared.Dtos;

namespace Taskify.Shared.Realtime;

public record TaskAssignedMessage(int TaskId, int ProjectId, UserDto? Assignee);
