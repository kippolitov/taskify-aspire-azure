using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Web.Components.Pages;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T020b — KanbanBoard renders 4 columns in correct order (read-only).</summary>
public class KanbanBoardTests : BunitContext
{
    [Fact]
    public void Board_RendersExactlyFourColumns()
    {
        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
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
        cut.WaitForElement(".kanban-board", TimeSpan.FromSeconds(3));

        var columns = cut.FindAll(".kanban-column");
        Assert.Equal(4, columns.Count);
    }

    [Fact]
    public void Board_ColumnLabelsAreInCorrectOrder()
    {
        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
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
        cut.WaitForElement(".kanban-board", TimeSpan.FromSeconds(3));

        var headers = cut.FindAll(".kanban-column__header");
        Assert.Contains("To Do", headers[0].TextContent);
        Assert.Contains("In Progress", headers[1].TextContent);
        Assert.Contains("In Review", headers[2].TextContent);
        Assert.Contains("Done", headers[3].TextContent);
    }

    [Fact]
    public void Board_TasksDistributedToCorrectColumns()
    {
        var handler = new TestApiHandler(
            new Dictionary<string, string>
            {
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
        cut.WaitForElement(".kanban-board", TimeSpan.FromSeconds(3));

        // SampleTasks: 1 in first column, 1 in second column
        var todoColumn = cut.Find("[aria-label='To Do tasks']");
        var inProgressColumn = cut.Find("[aria-label='In Progress tasks']");

        Assert.Contains("Set up CI pipeline", todoColumn.TextContent);
        Assert.Contains("Implement login", inProgressColumn.TextContent);
    }

    [Fact]
    public void Board_UnauthenticatedUser_RendersRedirectToHome()
    {
        var handler = new TestApiHandler(new Dictionary<string, string>());
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        // No current user set
        Services.AddSingleton(new IdentityService());
        Services.AddSingleton(new ApiClient(httpClient));
        Services.AddSingleton<BoardHubClient>(
            new BoardHubClient(new NullHttpMessageHandlerFactory())
        );

        JSInterop.Mode = JSRuntimeMode.Loose;
        var cut = Render<KanbanBoard>(p => p.Add(b => b.ProjectId, 1));

        // Should not show the board or any kanban columns
        Assert.Empty(cut.FindAll(".kanban-board"));
    }
}
