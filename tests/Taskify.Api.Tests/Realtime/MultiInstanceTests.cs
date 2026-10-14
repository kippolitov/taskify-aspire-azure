using System.Net.Http.Headers;
using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Enums;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Tests.Realtime;

/// <summary>
/// Two API instances share PostgreSQL and Redis. A change made through one instance reaches
/// a viewer connected to the other within 2 seconds (FR-032, SC-006).
/// </summary>
public class MultiInstanceTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    private const int Iterations = 20;

    [Fact]
    public async Task MoveThroughInstanceB_ReachesViewerOnInstanceA_EveryTime()
    {
        var redis = await SharedRedis.GetConnectionStringAsync();
        var instanceA = await fixture.CreateFactoryAsync(b =>
            b.UseSetting("ConnectionStrings:redis", redis)
        );
        var instanceB = await fixture.CreateFactoryAsync(b =>
            b.UseSetting("ConnectionStrings:redis", redis)
        );
        var token = await fixture.GetTokenAsync(1);

        await using var viewer = await HubTestClient.ConnectAsync(instanceA.Server, token);
        await viewer.JoinBoardAsync(1);
        var changer = instanceB.CreateClient();
        changer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );

        for (var i = 0; i < Iterations; i++)
        {
            var status = i % 2 == 0 ? ColumnStatus.ToDo : ColumnStatus.Done;
            var moved = viewer.WaitForAsync<TaskMovedMessage>(
                nameof(ITaskifyClient.TaskMoved),
                TimeSpan.FromSeconds(2)
            );

            (
                await changer.PatchAsJsonAsync(
                    "/api/tasks/1/status",
                    new { status },
                    TestJson.Options
                )
            ).EnsureSuccessStatusCode();

            var message = await moved;
            Assert.Equal(1, message.TaskId);
            Assert.Equal(status, message.ToStatus);
        }
    }
}
