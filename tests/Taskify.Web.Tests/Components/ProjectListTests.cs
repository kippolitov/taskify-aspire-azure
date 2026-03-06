using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Components.Pages;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T017a — ProjectList.razor bUnit tests.</summary>
public class ProjectListTests : BunitContext
{
    private void RegisterServices(UserDto? activeUser = null)
    {
        var projectsJson = TestApiHandler.Serialize(TestData.ThreeProjects);

        var handler = new TestApiHandler(
            new Dictionary<string, string> { ["api/projects"] = projectsJson }
        );
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        var identity = new IdentityService();
        if (activeUser is not null)
        {
            identity.SetUser(activeUser);
        }

        Services.AddScoped(_ => new ApiClient(httpClient));
        Services.AddScoped(_ => identity);
    }

    [Fact]
    public void ProjectList_UnauthenticatedUser_RendersRedirectToHome()
    {
        RegisterServices(activeUser: null);

        var cut = Render<ProjectList>();

        // Should show RedirectToHome, not the projects grid
        Assert.DoesNotContain("project-grid", cut.Markup);
    }

    [Fact]
    public void ProjectList_AuthenticatedUser_RendersThreeProjects()
    {
        RegisterServices(activeUser: TestData.FiveUsers[0]);

        var cut = Render<ProjectList>();

        cut.WaitForState(
            () => cut.FindAll(".project-card").Count == 3,
            timeout: TimeSpan.FromSeconds(3)
        );

        Assert.Equal(3, cut.FindAll(".project-card").Count);
    }

    [Fact]
    public void ProjectList_ShowsProjectNames()
    {
        RegisterServices(activeUser: TestData.FiveUsers[0]);

        var cut = Render<ProjectList>();

        cut.WaitForState(
            () => cut.FindAll(".project-card").Count == 3,
            timeout: TimeSpan.FromSeconds(3)
        );

        var names = cut.FindAll(".project-card__name").Select(e => e.TextContent).ToList();
        Assert.Contains("Mobile Relaunch", names);
        Assert.Contains("API Gateway v2", names);
        Assert.Contains("Design System", names);
    }

    [Fact]
    public void ProjectList_ProjectCards_HaveCorrectHref()
    {
        RegisterServices(activeUser: TestData.FiveUsers[0]);

        var cut = Render<ProjectList>();

        cut.WaitForState(
            () => cut.FindAll(".project-card").Count == 3,
            timeout: TimeSpan.FromSeconds(3)
        );

        var hrefs = cut.FindAll(".project-card").Select(e => e.GetAttribute("href")).ToList();

        Assert.Contains("/board/1", hrefs);
        Assert.Contains("/board/2", hrefs);
        Assert.Contains("/board/3", hrefs);
    }
}
