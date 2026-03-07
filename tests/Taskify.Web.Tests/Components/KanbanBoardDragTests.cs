using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Components.Pages;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T025c — KanbanBoard drag-and-drop callback tests.</summary>
public class KanbanBoardDragTests : BunitContext
{
    private sealed class DelayedDragApiHandler(
        Dictionary<string, string> responses,
        int delayMs
    ) : HttpMessageHandler
    {
        private readonly Dictionary<string, string> _responses = responses;
        private readonly int _delayMs = delayMs;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;

            foreach (var (pattern, json) in _responses)
            {
                if (!path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (_delayMs > 0 && request.Method == HttpMethod.Patch)
                {
                    await Task.Delay(_delayMs, cancellationToken);
                }

                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        json,
                        System.Text.Encoding.UTF8,
                        "application/json"
                    ),
                };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }
    }

    private async Task<IRenderedComponent<KanbanBoard>> RenderBoardAsync()
    {
        // Task 1 starts in the first column; the PATCH endpoint returns it as InProgress
        var movedTask = TestData.SampleTasks[0] with
        {
            Status = ColumnStatus.InProgress,
        };

        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
                ["/api/tasks/1/status"] = TestApiHandler.Serialize(movedTask),
                ["/api/projects/1/tasks"] = TestApiHandler.Serialize(TestData.SampleTasks),
                ["/api/projects/1"] = TestApiHandler.Serialize(TestData.ThreeProjects[0]),
                ["/api/users"] = TestApiHandler.Serialize(TestData.FiveUsers),
            }
        );

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var identity = new IdentityService();
        identity.SetUser(TestData.FiveUsers[0]);

        Services.AddSingleton(identity);
        Services.AddSingleton(new ApiClient(httpClient));
        Services.AddSingleton<BoardHubClient>(
            new BoardHubClient(new NullHttpMessageHandlerFactory())
        );

        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<KanbanBoard>(p => p.Add(b => b.ProjectId, 1));
        await cut.WaitForElementAsync(".kanban-board", TimeSpan.FromSeconds(3));
        return cut;
    }

    [Fact]
    public async Task OnTaskDropped_UpdatesStatusAndTotalsImmediately_BeforeApiCompletes()
    {
        var movedTask = TestData.SampleTasks[0] with { Status = ColumnStatus.InProgress };

        var handler = new DelayedDragApiHandler(
            new Dictionary<string, string>
            {
                ["/api/tasks/1/status"] = TestApiHandler.Serialize(movedTask),
                ["/api/projects/1/tasks"] = TestApiHandler.Serialize(TestData.SampleTasks),
                ["/api/projects/1"] = TestApiHandler.Serialize(TestData.ThreeProjects[0]),
                ["/api/users"] = TestApiHandler.Serialize(TestData.FiveUsers),
            },
            delayMs: 250
        );

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };
        var identity = new IdentityService();
        identity.SetUser(TestData.FiveUsers[0]);

        Services.AddSingleton(identity);
        Services.AddSingleton(new ApiClient(httpClient));
        Services.AddSingleton<BoardHubClient>(
            new BoardHubClient(new NullHttpMessageHandlerFactory())
        );

        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<KanbanBoard>(p => p.Add(b => b.ProjectId, 1));
        await cut.WaitForElementAsync(".kanban-board", TimeSpan.FromSeconds(3));

        var dropTask = cut.InvokeAsync(() => cut.Instance.OnTaskDropped("1", "ToDo", "InProgress"));

        cut.Render();

        var todoColumn = cut.Find("[aria-label='To Do tasks']");
        var inProgressColumn = cut.Find("[aria-label='In Progress tasks']");

        Assert.DoesNotContain("Set up CI pipeline", todoColumn.TextContent);
        Assert.Contains("Set up CI pipeline", inProgressColumn.TextContent);

        var headers = cut.FindAll(".kanban-column__header");
        Assert.Contains("ToDo0", System.Text.RegularExpressions.Regex.Replace(headers[0].TextContent, @"\s+", string.Empty));
        Assert.Contains("InProgress2", System.Text.RegularExpressions.Regex.Replace(headers[1].TextContent, @"\s+", string.Empty));

        await dropTask;
    }

    [Fact]
    public async Task OnTaskDropped_ValidMove_TaskMovesToTargetColumn()
    {
        var cut = await RenderBoardAsync();

        // Drag task 1 from the first column to InProgress
        await cut.InvokeAsync(() => cut.Instance.OnTaskDropped("1", "ToDo", "InProgress"));
        cut.Render();

        var inProgressColumn = cut.Find("[aria-label='In Progress tasks']");
        Assert.Contains("Set up CI pipeline", inProgressColumn.TextContent);
    }

    [Fact]
    public async Task OnTaskDropped_InvalidTaskId_DoesNotThrow()
    {
        var cut = await RenderBoardAsync();

        // A non-numeric task id should be silently ignored
        await cut.InvokeAsync(() =>
            cut.Instance.OnTaskDropped("not-a-number", "ToDo", "InProgress")
        );

        // Board should still be visible
        Assert.NotEmpty(cut.FindAll(".kanban-column"));
    }

    [Fact]
    public async Task OnTaskDropped_InvalidToColumn_DoesNotThrow()
    {
        var cut = await RenderBoardAsync();

        // Invalid column name should be silently ignored
        await cut.InvokeAsync(() => cut.Instance.OnTaskDropped("1", "ToDo", "NotAColumn"));

        Assert.NotEmpty(cut.FindAll(".kanban-column"));
    }

    [Fact]
    public async Task OnTaskDropped_SameColumn_DoesNotDuplicate()
    {
        var cut = await RenderBoardAsync();

        // Drop to the same column the task is already in (no-op)
        await cut.InvokeAsync(() => cut.Instance.OnTaskDropped("1", "ToDo", "ToDo"));
        cut.Render();

        // Task 1 should still only appear once in its original column (or InProgress after the mock returns InProgress)
        var allTaskCards = cut.FindAll("[data-task-id]");
        Assert.Equal(TestData.SampleTasks.Count, allTaskCards.Count);
    }
}
