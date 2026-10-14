using Taskify.Shared.Dtos;
using Taskify.Shared.Enums;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Services;

public class IdentityServiceTests
{
    [Fact]
    public void InitialState_IsNotAuthenticated()
    {
        var svc = TestIdentity.Create();

        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.CurrentUser);
    }

    [Fact]
    public async Task SignIn_SetsCurrentUserAndRaisesOnChange()
    {
        var svc = TestIdentity.Create();
        var user = new UserDto(1, "Alice", UserRole.Engineer);
        var changeCount = 0;
        svc.OnChange += () => changeCount++;

        await svc.SignInAsync(user);

        Assert.True(svc.IsAuthenticated);
        Assert.Equal(user, svc.CurrentUser);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task ClearUser_RemovesCurrentUserAndRaisesOnChange()
    {
        var svc = TestIdentity.Create();
        await svc.SignInAsync(new UserDto(2, "Bob", UserRole.ProductManager));
        var changeCount = 0;
        svc.OnChange += () => changeCount++;

        svc.ClearUser();

        Assert.False(svc.IsAuthenticated);
        Assert.Null(svc.CurrentUser);
        Assert.Equal(1, changeCount);
    }

    [Fact]
    public async Task SignIn_OverwritesPreviousUser()
    {
        var svc = TestIdentity.Create();
        await svc.SignInAsync(new UserDto(1, "Alice", UserRole.Engineer));
        var bob = new UserDto(2, "Bob", UserRole.ProductManager);

        await svc.SignInAsync(bob);

        Assert.Equal(bob, svc.CurrentUser);
        Assert.Equal("Bob", svc.CurrentUser!.DisplayName);
    }
}
