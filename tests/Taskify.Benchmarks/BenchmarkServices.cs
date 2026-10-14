using Microsoft.Extensions.DependencyInjection;
using Taskify.Api.Data;
using Taskify.Api.Observability;
using Taskify.Api.Services;

namespace Taskify.Benchmarks;

internal static class BenchmarkServices
{
    private static readonly IServiceProvider Services = new ServiceCollection()
        .AddLogging()
        .AddMetrics()
        .AddSingleton(TimeProvider.System)
        .AddSingleton<TaskifyMetrics>()
        .AddSignalR()
        .Services.BuildServiceProvider();

    /// <summary>
    /// A <see cref="TaskService"/> with the real hub context; with no connections, broadcasts
    /// go nowhere.
    /// </summary>
    public static TaskService CreateTaskService(TaskifyDbContext db) =>
        ActivatorUtilities.CreateInstance<TaskService>(Services, db);
}
