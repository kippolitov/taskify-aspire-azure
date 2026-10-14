using System.Net.Http.Json;
using System.Text.Json;
using Taskify.Api.Tests.Infrastructure;

namespace Taskify.Api.Tests.Realtime;

/// <summary>
/// A real subscriber on the hub receives what the API broadcasts after a change.
/// </summary>
public class BroadcastTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    private static readonly TimeSpan Delivery = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(1);

    private async Task MoveAsync(int taskId, string status)
    {
        var client = await fixture.CreateClientAsAsync(1);
        var response = await client.PatchAsJsonAsync($"/api/tasks/{taskId}/status", new { status });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task MoveStatus_BroadcastsTaskMoved_ToTheJoinedBoard()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(1);

        var received = hub.WaitForAsync<JsonElement>("TaskMoved", Delivery);
        await MoveAsync(1, "Done");
        var message = await received;

        Assert.Equal(1, message.GetProperty("taskId").GetInt32());
        Assert.Equal(1, message.GetProperty("projectId").GetInt32());
        Assert.Equal("Done", message.GetProperty("toStatus").GetString());
    }

    [Fact]
    public async Task MoveStatus_OnAnotherBoard_IsNotDelivered()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(2);

        var received = hub.WaitForAsync<JsonElement>("TaskMoved", Silence);
        await MoveAsync(2, "InReview");

        await Assert.ThrowsAsync<TimeoutException>(() => received);
    }

    [Fact]
    public async Task LeaveBoard_StopsDelivery()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(1);
        await hub.LeaveBoardAsync(1);

        var received = hub.WaitForAsync<JsonElement>("TaskMoved", Silence);
        await MoveAsync(3, "InProgress");

        await Assert.ThrowsAsync<TimeoutException>(() => received);
    }

    [Fact]
    public async Task JoinThenLeave_ThenJoinAgain_DeliversOnce()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(3);
        await hub.LeaveBoardAsync(3);
        await hub.JoinBoardAsync(3);

        var received = hub.WaitForAsync<JsonElement>("TaskMoved", Delivery);
        await MoveAsync(9, "Done");

        Assert.Equal(9, (await received).GetProperty("taskId").GetInt32());
    }
}
