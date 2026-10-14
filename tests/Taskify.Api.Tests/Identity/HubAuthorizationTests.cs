using System.Net;
using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Tests.Identity;

/// <summary>Subscribing to live board updates requires an identity (FR-028).</summary>
public class HubAuthorizationTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    [Fact]
    public async Task Connect_WithoutIdentity_IsRefusedWith401()
    {
        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            HubTestClient.ConnectAsync(fixture.Factory.Server, accessToken: null)
        );

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
    }

    [Fact]
    public async Task Connect_WithIdentity_ReceivesBoardUpdates()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(4)
        );
        await hub.JoinBoardAsync(1);
        var moved = hub.WaitForAsync<TaskMovedMessage>(
            nameof(ITaskifyClient.TaskMoved),
            TimeSpan.FromSeconds(2)
        );

        var client = await fixture.CreateClientAsAsync(4);
        (
            await client.PatchAsJsonAsync("/api/tasks/5/status", new { status = "InProgress" })
        ).EnsureSuccessStatusCode();

        Assert.Equal(5, (await moved).TaskId);
    }
}
