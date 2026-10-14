using System.Diagnostics.Metrics;

namespace Taskify.Api.Observability;

/// <summary>The API's own instruments, on meter <c>Taskify.Api</c>.</summary>
public sealed class TaskifyMetrics
{
    public const string MeterName = "Taskify.Api";

    private readonly Counter<long> _tasksMoved;
    private readonly Counter<long> _broadcastsFailed;

    public TaskifyMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _tasksMoved = meter.CreateCounter<long>(
            "taskify.tasks.moved",
            unit: "{move}",
            description: "Task Status changes."
        );
        _broadcastsFailed = meter.CreateCounter<long>(
            "taskify.broadcasts.failed",
            unit: "{message}",
            description: "Live updates that could not be sent after a saved change."
        );
    }

    /// <summary>Counts one Status change. A move to the same Status is not a change.</summary>
    public void TaskMoved() => _tasksMoved.Add(1);

    public void BroadcastFailed() => _broadcastsFailed.Add(1);
}
