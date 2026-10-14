using Microsoft.Extensions.DependencyInjection;
using Taskify.Api.Services;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Enums;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Tests.Data;

/// <summary>
/// A request cancelled before its save changes nothing and broadcasts nothing (FR-016).
/// </summary>
public class CancellationTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    [Fact]
    public async Task MoveTask_CancelledBeforeSave_ChangesNothing_AndBroadcastsNothing()
    {
        await using var hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await hub.JoinBoardAsync(1);
        var broadcast = hub.WaitForAsync<TaskMovedMessage>(
            nameof(ITaskifyClient.TaskMoved),
            TimeSpan.FromSeconds(1)
        );

        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await using (var scope = fixture.Factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<TaskService>();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.MoveTaskAsync(3, ColumnStatus.Done, cancelled.Token)
            );
        }

        await using var db = fixture.CreateDbContext();
        Assert.Equal(ColumnStatus.InProgress, (await db.TaskItems.FindAsync(3))!.Status);
        await Assert.ThrowsAsync<TimeoutException>(() => broadcast);
    }
}
