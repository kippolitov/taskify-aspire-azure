using System.Net.Http.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Shared.Realtime;

namespace Taskify.Api.Tests.Contract;

/// <summary>
/// Each <see cref="ITaskifyClient"/> message arrives with the fields of the shared
/// message record (FR-018, contracts/signalr-hub.md).
/// </summary>
[Trait("Category", "Contract")]
public class HubContractTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private static readonly TimeSpan Delivery = TimeSpan.FromSeconds(2);
    private HttpClient _client = null!;
    private HubTestClient _hub = null!;

    public async Task InitializeAsync()
    {
        _client = await fixture.CreateClientAsAsync(1);
        _hub = await HubTestClient.ConnectAsync(
            fixture.Factory.Server,
            await fixture.GetTokenAsync(1)
        );
        await _hub.JoinBoardAsync(1);
    }

    public async Task DisposeAsync() => await _hub.DisposeAsync();

    private async Task<CommentDto> AddCommentAsync(int taskId, string text)
    {
        var response = await _client.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task TaskMoved_CarriesStatusesAsColumnStatus()
    {
        var received = _hub.WaitForAsync<TaskMovedMessage>(
            nameof(ITaskifyClient.TaskMoved),
            Delivery
        );

        (
            await _client.PatchAsJsonAsync("/api/tasks/3/status", new { status = "InReview" })
        ).EnsureSuccessStatusCode();
        var message = await received;

        Assert.Equal(3, message.TaskId);
        Assert.Equal(1, message.ProjectId);
        Assert.Equal(ColumnStatus.InProgress, message.FromStatus);
        Assert.Equal(ColumnStatus.InReview, message.ToStatus);
        Assert.NotEqual(default, message.MovedAt);
    }

    [Fact]
    public async Task TaskAssigned_CarriesTheNewAssignee()
    {
        var received = _hub.WaitForAsync<TaskAssignedMessage>(
            nameof(ITaskifyClient.TaskAssigned),
            Delivery
        );

        (
            await _client.PatchAsJsonAsync("/api/tasks/5/assignee", new { assigneeId = 4 })
        ).EnsureSuccessStatusCode();
        var message = await received;

        Assert.Equal(5, message.TaskId);
        Assert.Equal(1, message.ProjectId);
        Assert.Equal(4, message.Assignee?.Id);
        Assert.Equal("Marcus Johnson", message.Assignee?.DisplayName);
    }

    [Fact]
    public async Task TaskCreated_CarriesTheTask()
    {
        var received = _hub.WaitForAsync<TaskDto>(nameof(ITaskifyClient.TaskCreated), Delivery);

        (
            await _client.PostAsJsonAsync("/api/projects/1/tasks", new { title = "Contract task" })
        ).EnsureSuccessStatusCode();
        var message = await received;

        Assert.Equal("Contract task", message.Title);
        Assert.Equal(1, message.ProjectId);
        Assert.Equal(ColumnStatus.ToDo, message.Status);
    }

    [Fact]
    public async Task CommentAdded_CarriesTheComment()
    {
        var received = _hub.WaitForAsync<CommentAddedMessage>(
            nameof(ITaskifyClient.CommentAdded),
            Delivery
        );

        var comment = await AddCommentAsync(1, "Contract comment");
        var message = await received;

        Assert.Equal(1, message.TaskId);
        Assert.Equal(comment.Id, message.Comment.Id);
        Assert.Equal("Contract comment", message.Comment.Text);
        Assert.Equal(1, message.Comment.Author.Id);
    }

    [Fact]
    public async Task CommentEdited_CarriesTheNewText()
    {
        var comment = await AddCommentAsync(2, "Before");
        var received = _hub.WaitForAsync<CommentEditedMessage>(
            nameof(ITaskifyClient.CommentEdited),
            Delivery
        );

        (
            await _client.PutAsJsonAsync($"/api/comments/{comment.Id}", new { text = "After" })
        ).EnsureSuccessStatusCode();
        var message = await received;

        Assert.Equal(2, message.TaskId);
        Assert.Equal(comment.Id, message.CommentId);
        Assert.Equal("After", message.NewText);
        Assert.NotNull(message.EditedAt);
    }

    [Fact]
    public async Task CommentDeleted_CarriesTheIds()
    {
        var comment = await AddCommentAsync(4, "Doomed");
        var received = _hub.WaitForAsync<CommentDeletedMessage>(
            nameof(ITaskifyClient.CommentDeleted),
            Delivery
        );

        (await _client.DeleteAsync($"/api/comments/{comment.Id}")).EnsureSuccessStatusCode();
        var message = await received;

        Assert.Equal(4, message.TaskId);
        Assert.Equal(comment.Id, message.CommentId);
    }
}
