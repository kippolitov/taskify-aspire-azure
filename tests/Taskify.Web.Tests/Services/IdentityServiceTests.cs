using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Services;

namespace Taskify.Web.Tests.Services;

public class IdentityServiceTests
{
    [Fact]
    public void InitialState_IsNotAuthenticated()
    {
        var svc = new IdentityService();

        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.CurrentUser);
    }

    [Fact]
    public void SetUser_SetsCurrentUserAndRaisesOnChange()
    {
        var svc = new IdentityService();
        var user = new UserDto(1, "Alice", UserRole.Engineer);
        var changeCount = 0;
        svc.OnChange += () => changeCount++;

        svc.SetUser(user);

        Assert.True(svc.IsAuthenticated);
        Assert.Equal(user, svc.CurrentUser);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void ClearUser_RemovesCurrentUserAndRaisesOnChange()
    {
        var svc = new IdentityService();
        svc.SetUser(new UserDto(2, "Bob", UserRole.ProductManager));
        var changeCount = 0;
        svc.OnChange += () => changeCount++;

        svc.ClearUser();

        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.CurrentUser);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public void SetUser_OverwritesPreviousUser()
    {
        var svc = new IdentityService();
        svc.SetUser(new UserDto(1, "Alice", UserRole.Engineer));
        var bob = new UserDto(2, "Bob", UserRole.ProductManager);

        svc.SetUser(bob);

        Assert.Equal(bob, svc.CurrentUser);
        Assert.Equal("Bob", svc.CurrentUser!.DisplayName);
    }
}
