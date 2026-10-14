using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Identity;

/// <summary>
/// The caller comes from the identity alone, never from the request (FR-023–FR-027, SC-005).
/// </summary>
public class AuthorizationTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    public static TheoryData<string, string, object?> ProtectedOperations =>
        new()
        {
            { "GET", "/api/users/1", null },
            { "GET", "/api/projects", null },
            { "GET", "/api/projects/1", null },
            { "GET", "/api/projects/1/tasks", null },
            { "GET", "/api/tasks/1", null },
            { "POST", "/api/projects/1/tasks", new { title = "Sneaky" } },
            { "PUT", "/api/tasks/1", new { title = "Sneaky", description = (string?)null } },
            { "PATCH", "/api/tasks/1/assignee", new { assigneeId = 2 } },
            { "PATCH", "/api/tasks/1/status", new { status = "ToDo" } },
            { "GET", "/api/tasks/1/comments", null },
            { "POST", "/api/tasks/1/comments", new { text = "Sneaky" } },
            { "PUT", "/api/comments/1", new { text = "Sneaky" } },
            { "DELETE", "/api/comments/1", null },
            { "GET", "/api/notifications", null },
        };

    private async Task<CommentDto> AddCommentAsAsync(int userId, string text)
    {
        var client = await fixture.CreateClientAsAsync(userId);
        var response = await client.PostAsJsonAsync("/api/tasks/2/comments", new { text });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options))!;
    }

    [Theory]
    [MemberData(nameof(ProtectedOperations))]
    public async Task WithoutIdentity_Returns401_AndChangesNothing(
        string method,
        string path,
        object? body
    )
    {
        string before;
        await using (var db = fixture.CreateDbContext())
        {
            before = await SnapshotAsync(db);
        }

        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: TestJson.Options);
        }

        var response = await fixture.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var check = fixture.CreateDbContext();
        Assert.Equal(before, await SnapshotAsync(check));
    }

    [Fact]
    public async Task UserList_IsOpen()
    {
        var response = await fixture.CreateClient().GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AddComment_AuthorComesFromIdentity_NotFromBody()
    {
        var client = await fixture.CreateClientAsAsync(2);

        var response = await client.PostAsJsonAsync(
            "/api/tasks/1/comments",
            new { text = "hi", authorId = 1 }
        );

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var comment = await response.Content.ReadFromJsonAsync<CommentDto>(TestJson.Options);
        Assert.Equal(2, comment!.Author.Id);
    }

    [Fact]
    public async Task EditAndDelete_ByAnotherUser_Return403_AndLeaveCommentUnchanged()
    {
        var comment = await AddCommentAsAsync(2, "Written by User 2");
        var intruder = await fixture.CreateClientAsAsync(3);

        var edit = await intruder.PutAsJsonAsync(
            $"/api/comments/{comment.Id}",
            new { text = "Hijacked" }
        );
        var delete = await intruder.DeleteAsync($"/api/comments/{comment.Id}");

        await HttpAssert.ProblemAsync(edit, HttpStatusCode.Forbidden);
        await HttpAssert.ProblemAsync(delete, HttpStatusCode.Forbidden);
        await using var db = fixture.CreateDbContext();
        Assert.Equal("Written by User 2", (await db.Comments.FindAsync(comment.Id))!.Text);
    }

    [Fact]
    public async Task EditAndDelete_ByAuthor_Succeed()
    {
        var comment = await AddCommentAsAsync(2, "Mine");
        var author = await fixture.CreateClientAsAsync(2);

        var edit = await author.PutAsJsonAsync(
            $"/api/comments/{comment.Id}",
            new { text = "Edited" }
        );
        var delete = await author.DeleteAsync($"/api/comments/{comment.Id}");

        Assert.Equal(HttpStatusCode.OK, edit.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
    }

    /// <summary>Everything an unauthenticated request could have changed.</summary>
    private static async Task<string> SnapshotAsync(Api.Data.TaskifyDbContext db)
    {
        var tasks = await db
            .TaskItems.AsNoTracking()
            .OrderBy(t => t.Id)
            .Select(t =>
                $"{t.Id}:{t.Title}:{t.Description}:{t.Status}:{t.AssigneeId}:{t.UpdatedAt:O}"
            )
            .ToListAsync();
        var comments = await db
            .Comments.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => $"{c.Id}:{c.Text}")
            .ToListAsync();
        return string.Join("|", tasks.Concat(comments));
    }
}
