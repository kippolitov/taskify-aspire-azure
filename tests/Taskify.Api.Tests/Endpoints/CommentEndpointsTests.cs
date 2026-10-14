using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Endpoints;

/// <summary>
/// The seed has no Comments, so each test adds the Comment it needs.
/// </summary>
public class CommentEndpointsTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await fixture.CreateClientAsAsync(1);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<CommentDto> AddCommentAsync(int taskId, int authorId, string text)
    {
        var author = await fixture.CreateClientAsAsync(authorId);
        var response = await author.PostAsJsonAsync($"/api/tasks/{taskId}/comments", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options))!;
    }

    [Fact]
    public async Task GetTaskComments_ExistingTask_ReturnsComments_OldestFirst()
    {
        var first = await AddCommentAsync(2, 1, "First");
        var second = await AddCommentAsync(2, 2, "Second");

        var comments = await _client.GetFromJsonAsync<List<CommentDto>>(
            "/api/tasks/2/comments",
            TestJson.Options
        );

        Assert.Equal([first.Id, second.Id], comments!.Select(c => c.Id));
    }

    [Fact]
    public async Task GetTaskComments_NonExistingTask_Returns404()
    {
        var response = await _client.GetAsync("/api/tasks/999/comments");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AddComment_ValidRequest_Returns201_AndPersists()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tasks/1/comments",
            new { text = "A new comment" }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options);
        Assert.NotNull(comment);
        Assert.Equal("A new comment", comment.Text);
        Assert.Equal(1, comment.Author.Id);
        Assert.Equal(
            $"/api/tasks/1/comments/{comment.Id}",
            response.Headers.Location?.OriginalString
        );

        await using var db = fixture.CreateDbContext();
        Assert.True(await db.Comments.AnyAsync(c => c.Id == comment.Id && c.TaskItemId == 1));
    }

    [Fact]
    public async Task AddComment_NonExistingTask_Returns404()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/tasks/999/comments",
            new { text = "Text" }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task EditComment_ByAuthor_ReturnsUpdatedComment_AndPersists()
    {
        var comment = await AddCommentAsync(3, 1, "Original");

        var response = await _client.PutAsJsonAsync(
            $"/api/comments/{comment.Id}",
            new { text = "Edited text" }
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var edited = await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options);
        Assert.Equal("Edited text", edited!.Text);
        Assert.NotNull(edited.EditedAt);

        await using var db = fixture.CreateDbContext();
        Assert.Equal("Edited text", (await db.Comments.FindAsync(comment.Id))!.Text);
    }

    [Fact]
    public async Task EditComment_ByNonAuthor_Returns403_AndLeavesCommentUnchanged()
    {
        var comment = await AddCommentAsync(3, 1, "Mine");

        var other = await fixture.CreateClientAsAsync(2);
        var response = await other.PutAsJsonAsync(
            $"/api/comments/{comment.Id}",
            new { text = "Hacked edit" }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.Forbidden);
        await using var db = fixture.CreateDbContext();
        Assert.Equal("Mine", (await db.Comments.FindAsync(comment.Id))!.Text);
    }

    [Fact]
    public async Task EditComment_NonExistingComment_Returns404()
    {
        var response = await _client.PutAsJsonAsync("/api/comments/999", new { text = "Text" });

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteComment_ByAuthor_Returns204_AndRemovesComment()
    {
        var comment = await AddCommentAsync(4, 1, "Short-lived");

        var response = await _client.DeleteAsync($"/api/comments/{comment.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await using var db = fixture.CreateDbContext();
        Assert.Null(await db.Comments.FindAsync(comment.Id));
    }

    [Fact]
    public async Task DeleteComment_ByNonAuthor_Returns403_AndKeepsComment()
    {
        var comment = await AddCommentAsync(4, 1, "Keep me");

        var other = await fixture.CreateClientAsAsync(2);
        var response = await other.DeleteAsync($"/api/comments/{comment.Id}");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.Forbidden);
        await using var db = fixture.CreateDbContext();
        Assert.NotNull(await db.Comments.FindAsync(comment.Id));
    }

    [Fact]
    public async Task DeleteComment_NonExistingComment_Returns404()
    {
        var response = await _client.DeleteAsync("/api/comments/999");

        await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
    }
}
