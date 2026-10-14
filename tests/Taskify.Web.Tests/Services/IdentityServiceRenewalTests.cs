using Microsoft.Extensions.Time.Testing;
using Taskify.Web.Services;
using Taskify.Web.Tests.Helpers;

namespace Taskify.Web.Tests.Services;

/// <summary>
/// The web app obtains, holds and silently renews the identity (FR-029).
/// </summary>
public class IdentityServiceRenewalTests
{
    private readonly FakeTimeProvider _time = new(DateTimeOffset.UtcNow);
    private readonly FakeTokenEndpoint _endpoint;
    private readonly IdentityService _identity;

    public IdentityServiceRenewalTests()
    {
        _endpoint = new FakeTokenEndpoint(_time);
        _identity = new IdentityService(new FakeHttpClientFactory(_endpoint), _time);
    }

    [Fact]
    public async Task SignIn_ObtainsAToken()
    {
        await _identity.SignInAsync(TestData.FiveUsers[1]);

        Assert.True(_identity.IsAuthenticated);
        Assert.Equal(1, _endpoint.Calls);
        Assert.Equal("token-1", await _identity.GetAccessTokenAsync());
    }

    [Fact]
    public async Task GetAccessToken_WithMoreThanTwoMinutesLeft_ReturnsTheHeldToken()
    {
        await _identity.SignInAsync(TestData.FiveUsers[1]);

        _time.Advance(TimeSpan.FromMinutes(12));

        Assert.Equal("token-1", await _identity.GetAccessTokenAsync());
        Assert.Equal(1, _endpoint.Calls);
    }

    [Fact]
    public async Task GetAccessToken_WithLessThanTwoMinutesLeft_RenewsOnce_ForConcurrentCallers()
    {
        await _identity.SignInAsync(TestData.FiveUsers[1]);
        _endpoint.Delay = TimeSpan.FromMilliseconds(100);

        _time.Advance(TimeSpan.FromMinutes(13.5));
        var tokens = await Task.WhenAll(
            Enumerable.Range(0, 10).Select(_ => _identity.GetAccessTokenAsync())
        );

        Assert.Equal(2, _endpoint.Calls);
        Assert.All(tokens, t => Assert.Equal("token-2", t));
    }

    [Fact]
    public async Task ClearUser_DropsTheToken()
    {
        await _identity.SignInAsync(TestData.FiveUsers[1]);

        _identity.ClearUser();

        Assert.False(_identity.IsAuthenticated);
        Assert.Null(await _identity.GetAccessTokenAsync());
    }

    [Fact]
    public async Task SignIn_RefusedByTheServer_LeavesNoUser()
    {
        var unknown = TestData.FiveUsers[0] with { Id = FakeTokenEndpoint.UnknownUserId };

        await Assert.ThrowsAsync<HttpRequestException>(() => _identity.SignInAsync(unknown));

        Assert.False(_identity.IsAuthenticated);
        Assert.Null(await _identity.GetAccessTokenAsync());
    }
}
