using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Taskify.Api.Tests.Infrastructure;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Tests.Identity;

/// <summary>The server issues a short-lived identity for an existing User (FR-021, FR-022).</summary>
public class TokenEndpointTests(TaskifyApiFixture fixture) : IClassFixture<TaskifyApiFixture>
{
    /// <summary>Token lifetime plus the default 5-minute clock skew, plus one minute.</summary>
    private static readonly TimeSpan PastExpiry = TimeSpan.FromMinutes(15 + 5 + 1);

    private Task<HttpResponseMessage> GetProjectsAsync(string token)
    {
        var client = fixture.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client.GetAsync("/api/projects");
    }

    [Fact]
    public async Task Issue_ExistingUser_Returns15MinuteTokenForThatUser()
    {
        var response = await fixture
            .CreateClient()
            .PostAsJsonAsync("/api/auth/token", new { userId = 2 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(TestJson.Options);
        Assert.NotNull(token);
        Assert.Equal(fixture.Time.GetUtcNow().AddMinutes(15), token.ExpiresAt);
        Assert.Equal("2", new JsonWebToken(token.AccessToken).Subject);
        Assert.Equal(HttpStatusCode.OK, (await GetProjectsAsync(token.AccessToken)).StatusCode);
    }

    [Fact]
    public async Task AlteredSignature_IsRejected()
    {
        var token = await fixture.GetTokenAsync(2);
        var signatureStart = token.LastIndexOf('.') + 1;
        var altered =
            token[..signatureStart]
            + (token[signatureStart] == 'A' ? 'B' : 'A')
            + token[(signatureStart + 1)..];

        var response = await GetProjectsAsync(altered);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Issue_NonExistingUser_Returns404_WithoutToken()
    {
        var response = await fixture
            .CreateClient()
            .PostAsJsonAsync("/api/auth/token", new { userId = 999 });

        var problem = await HttpAssert.ProblemAsync(response, HttpStatusCode.NotFound);
        Assert.False(problem.TryGetProperty("accessToken", out _));
    }

    [Fact]
    public async Task Issue_WithoutUserId_Returns400()
    {
        var response = await fixture.CreateClient().PostAsJsonAsync("/api/auth/token", new { });

        await HttpAssert.ProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var token = await fixture.GetTokenAsync(2);

        fixture.Time.Advance(PastExpiry);
        var response = await GetProjectsAsync(token);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
