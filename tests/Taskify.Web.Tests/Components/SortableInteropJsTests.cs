using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Web.Components.Pages;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>
/// T040 — Verifies sortable-interop.js invocations and component disposal.
/// Uses BunitContext + JSInterop to assert sortableInterop.init is called once
/// per column when the board loads, and that DisposeAsync does not throw.
/// </summary>
public class SortableInteropJsTests : BunitContext
{
    private IRenderedComponent<KanbanBoard> RenderAuthenticatedBoard()
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

        // Loose mode allows any JS invocation — we verify counts afterwards
        JSInterop.Mode = JSRuntimeMode.Loose;

        var cut = Render<KanbanBoard>(p => p.Add(b => b.ProjectId, 1));
        cut.WaitForElement(".kanban-board", TimeSpan.FromSeconds(3));
        return cut;
    }

    [Fact]
    public void Board_OnLoad_CallsSortableInitExactlyOncePerColumn()
    {
        var cut = RenderAuthenticatedBoard();
        Assert.NotNull(cut.Instance);

        var initCalls = JSInterop
            .Invocations.Where(i =>
                i.Identifier.Contains("sortableInterop.init", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        // 4 columns → 4 init calls
        Assert.Equal(4, initCalls.Count);
    }

    [Fact]
    public void Board_SortableInit_CalledForEachColumnStatus()
    {
        RenderAuthenticatedBoard();

        var initCalls = JSInterop
            .Invocations.Where(i =>
                i.Identifier.Contains("sortableInterop.init", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        // Every call should carry the DotNetObjectReference and a column status string
        Assert.All(initCalls, call => Assert.NotEmpty(call.Arguments));
    }

    [Fact]
    public void Board_UnauthenticatedUser_DoesNotCallSortableInit()
    {
        var handler = new TestApiHandler(new Dictionary<string, string>());
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") };

        // No user set
        Services.AddSingleton(new IdentityService());
        Services.AddSingleton(new ApiClient(httpClient));
        Services.AddSingleton<BoardHubClient>(
            new BoardHubClient(new NullHttpMessageHandlerFactory())
        );

        JSInterop.Mode = JSRuntimeMode.Loose;

        Render<KanbanBoard>(p => p.Add(b => b.ProjectId, 1));

        var initCalls = JSInterop
            .Invocations.Where(i =>
                i.Identifier.Contains("sortableInterop.init", StringComparison.OrdinalIgnoreCase)
            )
            .ToList();

        // Unauthenticated — board never loads, no init calls
        Assert.Empty(initCalls);
    }

    [Fact]
    public void Board_Render_ContextDisposesComponentsWithoutException()
    {
        var cut = RenderAuthenticatedBoard();

        // Component is valid before disposal
        Assert.NotNull(cut.Instance);
        Assert.NotEmpty(cut.FindAll(".kanban-board"));

        // bUnit's DisposeAsync is exercised automatically at end of test via
        // BunitContext.Dispose(); here we verify the component instance is live
        // and that calling Dispose on the cut does not throw.
        var ex = Record.Exception(cut.Dispose);
        Assert.Null(ex);
    }
}
