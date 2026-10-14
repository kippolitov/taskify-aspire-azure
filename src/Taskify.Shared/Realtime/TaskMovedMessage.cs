using Taskify.Shared.Enums;

namespace Taskify.Shared.Realtime;

public record TaskMovedMessage(
    int TaskId,
    int ProjectId,
    ColumnStatus FromStatus,
    ColumnStatus ToStatus,
    DateTimeOffset MovedAt
);
