using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Enums;

namespace Taskify.Api.Tests.Endpoints;

/// <summary>
/// Invalid input never reaches a handler: 400 Problem Details, keyed by field (FR-004, FR-011).
/// </summary>
public class ValidationTests(TaskifyApiFixture fixture)
    : IClassFixture<TaskifyApiFixture>,
        IAsyncLifetime
{
    private static readonly string Title301 = new('t', 301);
    private static readonly string Text10001 = new('c', 10_001);

    private HttpClient _client = null!;

    public async Task InitializeAsync() => _client = await fixture.CreateClientAsAsync(1);

    public Task DisposeAsync() => Task.CompletedTask;

    public static TheoryData<string, string, object, string> InvalidInputs =>
        new()
        {
            { "POST", "/api/projects/1/tasks", new { title = "" }, "Title" },
            { "POST", "/api/projects/1/tasks", new { title = Title301 }, "Title" },
            { "POST", "/api/projects/1/tasks", new { description = "No title" }, "Title" },
            { "PUT", "/api/tasks/1", new { title = "", description = (string?)null }, "Title" },
            {
                "PUT",
                "/api/tasks/1",
                new { title = Title301, description = (string?)null },
                "Title"
            },
            { "PATCH", "/api/tasks/1/status", new { status = 7 }, "Status" },
            { "POST", "/api/tasks/1/comments", new { text = "" }, "Text" },
            { "POST", "/api/tasks/1/comments", new { text = Text10001 }, "Text" },
            { "PUT", "/api/comments/1", new { text = "" }, "Text" },
            { "PUT", "/api/comments/1", new { text = Text10001 }, "Text" },
        };

    private Task<HttpResponseMessage> SendAsync(string method, string path, object body) =>
        _client.SendAsync(
            new HttpRequestMessage(new HttpMethod(method), path)
            {
                Content = JsonContent.Create(body, options: TestJson.Options),
            }
        );

    private static IEnumerable<string> Keys(JsonElement element) =>
        element.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);

    [Theory]
    [MemberData(nameof(InvalidInputs))]
    public async Task BrokenRule_Returns400_WithErrorsKeyedByTheField(
        string method,
        string path,
        object body,
        string field
    )
    {
        var response = await SendAsync(method, path, body);

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.BadRequest);
        var errors = problem.GetProperty("errors").EnumerateObject().Select(e => e.Name);
        Assert.Contains(field, errors, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EveryValidationFailure_HasTheSameTopLevelKeys()
    {
        var shapes = new List<string>();
        foreach (var row in InvalidInputs)
        {
            var (method, path, body) = ((string)row[0], (string)row[1], row[2]);
            var problem = await HttpAssert.ProblemAsync(
                await SendAsync(method, path, body),
                HttpStatusCode.BadRequest
            );
            shapes.Add(string.Join(",", Keys(problem)));
        }

        Assert.Single(shapes.Distinct());
    }

    [Fact]
    public async Task MoveStatus_UnknownStatusName_Returns400_AndLeavesTaskUnchanged()
    {
        DateTimeOffset updatedBefore;
        await using (var db = fixture.CreateDbContext())
        {
            updatedBefore = (await db.TaskItems.FindAsync(1))!.UpdatedAt;
        }

        var response = await _client.PatchAsJsonAsync(
            "/api/tasks/1/status",
            new { status = "Archived" }
        );

        await HttpAssert.ProblemAsync(response, HttpStatusCode.BadRequest);
        await using var check = fixture.CreateDbContext();
        var task = (await check.TaskItems.FindAsync(1))!;
        Assert.Equal(ColumnStatus.Done, task.Status);
        Assert.Equal(updatedBefore, task.UpdatedAt);
    }

    [Fact]
    public async Task CreateTask_WithoutTitle_Returns400_AndCreatesNothing()
    {
        int countBefore;
        await using (var db = fixture.CreateDbContext())
        {
            countBefore = db.TaskItems.Count();
        }

        var response = await _client.PostAsJsonAsync(
            "/api/projects/1/tasks",
            new { description = "No title" }
        );

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.BadRequest);
        Assert.Contains(
            "Title",
            problem.GetProperty("errors").EnumerateObject().Select(e => e.Name),
            StringComparer.OrdinalIgnoreCase
        );
        await using var check = fixture.CreateDbContext();
        Assert.Equal(countBefore, check.TaskItems.Count());
    }
}
