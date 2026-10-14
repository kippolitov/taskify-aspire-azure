using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Taskify.Api.Data.Entities;
using Taskify.Api.Services;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Data;

/// <summary>
/// The task list counts Comments in SQL and reads no Comment content (FR-014).
/// </summary>
public class TaskListQueryTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    [Fact]
    public async Task ProjectTasksQuery_CountsCommentsInSql_AndSelectsNoCommentColumns()
    {
        await using var db = fixture.CreateDbContext();

        var sql = TaskService.ProjectTasksQuery(db, 1).ToQueryString();

        Assert.Contains("count(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("c.text", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TaskList_CommentCount_MatchesTheCommentRows()
    {
        await using (var db = fixture.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            db.Comments.AddRange(
                new Comment
                {
                    TaskItemId = 1,
                    AuthorId = 1,
                    Text = "One",
                    CreatedAt = now,
                },
                new Comment
                {
                    TaskItemId = 1,
                    AuthorId = 2,
                    Text = "Two",
                    CreatedAt = now,
                }
            );
            await db.SaveChangesAsync();
        }

        var client = await fixture.CreateClientAsAsync(1);
        var tasks = await client.GetFromJsonAsync<List<TaskDto>>(
            "/api/projects/1/tasks",
            TestJson.Options
        );

        await using var check = fixture.CreateDbContext();
        var rows = await check.Comments.CountAsync(c => c.TaskItemId == 1);
        Assert.Equal(rows, tasks!.Single(t => t.Id == 1).CommentCount);
    }
}
