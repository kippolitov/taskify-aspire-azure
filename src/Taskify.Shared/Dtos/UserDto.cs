using Taskify.Shared.Enums;

namespace Taskify.Shared.Dtos;

public record UserDto(int Id, string DisplayName, UserRole Role);
