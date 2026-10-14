using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Realtime;

/// <summary>
/// When the relay between instances is down, a change is still saved and succeeds; only
/// its live update is lost (spec edge case).
/// </summary>
public class BroadcastFailureTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    private async Task<HttpClient> CreateClientWithRedisDownAsync()
    {
        var factory = await fixture.CreateFactoryAsync(b =>
            b.UseSetting(
                "ConnectionStrings:redis",
                "127.0.0.1:1,abortConnect=false,connectTimeout=500"
            )
        );
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await fixture.GetTokenAsync(1)
        );
        return client;
    }

    [Fact]
    public async Task MoveStatus_WithRedisUnavailable_StillSavesAndSucceeds()
    {
        var client = await CreateClientWithRedisDownAsync();

        var response = await client.PatchAsJsonAsync(
            "/api/tasks/2/status",
            new { status = "Done" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Equal(ColumnStatus.Done, (await db.TaskItems.FindAsync(2))!.Status);
    }

    [Fact]
    public async Task AddComment_WithRedisUnavailable_StillSavesAndSucceeds()
    {
        var client = await CreateClientWithRedisDownAsync();

        var response = await client.PostAsJsonAsync(
            "/api/tasks/3/comments",
            new { text = "Saved without a live update" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.True(await db.Comments.AnyAsync(c => c.Text == "Saved without a live update"));
    }
}
