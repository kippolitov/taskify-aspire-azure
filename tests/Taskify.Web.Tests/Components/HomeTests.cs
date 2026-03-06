using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Taskify.Web.Components.Pages;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Components;

/// <summary>T013a — UserSelection (Home.razor) bUnit tests.</summary>
public class HomeTests : BunitContext
{
    private void RegisterServices(List<Shared.Dtos.UserDto>? overrideUsers = null)
    {
        var users = overrideUsers ?? TestData.FiveUsers;
        var usersJson = TestApiHandler.Serialize(users);

        var handler = new TestApiHandler(
            new Dictionary<string, string> { ["api/users"] = usersJson }
        );
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost") };

        Services.AddScoped(_ => new ApiClient(httpClient));
        Services.AddScoped<IdentityService>();
    }

    [Fact]
    public void Home_RendersExactlyFiveUserCards()
    {
        RegisterServices();

        var cut = Render<Home>();

        cut.WaitForState(
            () => cut.FindAll(".user-card").Count == 5,
            timeout: TimeSpan.FromSeconds(3)
        );

        Assert.Equal(5, cut.FindAll(".user-card").Count);
    }

    [Fact]
    public void Home_ShowsUserNamesAndRoles()
    {
        RegisterServices();

        var cut = Render<Home>();

        cut.WaitForState(
            () => cut.FindAll(".user-card").Count == 5,
            timeout: TimeSpan.FromSeconds(3)
        );

        var names = cut.FindAll(".user-card__name").Select(e => e.TextContent).ToList();
        Assert.Contains("Alice Chen", names);
        Assert.Contains("Bob Kim", names);
        Assert.Contains("Priya Sharma", names);
        Assert.Contains("David Lee", names);
        Assert.Contains("Sofia Reyes", names);
    }

    [Fact]
    public void Home_ClickingUserCard_SetsIdentityCurrentUser()
    {
        RegisterServices();
        var identity = new IdentityService();
        Services.AddScoped(_ => identity);

        var cut = Render<Home>();
        cut.WaitForState(
            () => cut.FindAll(".user-card").Count == 5,
            timeout: TimeSpan.FromSeconds(3)
        );

        cut.FindAll(".user-card")[0].Click();

        Assert.True(identity.IsAuthenticated);
        Assert.NotNull(identity.CurrentUser);
        Assert.Equal("Alice Chen", identity.CurrentUser.DisplayName);
    }

    [Fact]
    public void Home_ClickingUserCard_NavigatesToProjects()
    {
        RegisterServices();

        var cut = Render<Home>();
        cut.WaitForState(
            () => cut.FindAll(".user-card").Count == 5,
            timeout: TimeSpan.FromSeconds(3)
        );

        cut.FindAll(".user-card")[0].Click();

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.EndsWith("/projects", nav.Uri);
    }
}
