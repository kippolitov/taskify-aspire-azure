using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Taskify.Api.Tests.Infrastructure;

namespace Taskify.Api.Tests.Observability;

/// <summary>Every Status change counts once; a move to the same Status does not (FR-039, SC-010).</summary>
public class TaskMoveMetricTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    private MetricCollector<long> Collect() =>
        new(
            fixture.Factory.Services.GetRequiredService<IMeterFactory>(),
            "Taskify.Api",
            "taskify.tasks.moved"
        );

    private static async Task MoveAsync(HttpClient client, int taskId, string status) =>
        (
            await client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new { status })
        ).EnsureSuccessStatusCode();

    private static long Total(MetricCollector<long> collector) =>
        collector.GetMeasurementSnapshot().Sum(m => m.Value);

    [Fact]
    public async Task FiveStatusChanges_CountFive()
    {
        var client = await fixture.CreateClientAsAsync(1);
        using var collector = Collect();

        // Task 3 starts In Progress.
        foreach (var status in new[] { "InReview", "Done", "ToDo", "InProgress", "InReview" })
        {
            await MoveAsync(client, 3, status);
        }

        Assert.Equal(5, Total(collector));
    }

    [Fact]
    public async Task MoveToTheSameStatus_CountsNothing()
    {
        var client = await fixture.CreateClientAsAsync(1);
        using var collector = Collect();

        // Task 9 starts Done.
        await MoveAsync(client, 9, "Done");

        Assert.Equal(0, Total(collector));
    }
}
