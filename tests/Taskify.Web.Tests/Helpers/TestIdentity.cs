using System.Net;
using System.Net.Http.Json;
using Taskify.Shared;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;
using Taskify.Web.Services;

namespace Taskify.Web.Tests.Helpers;

/// <summary>Builds <see cref="IdentityService"/> instances backed by a fake token endpoint.</summary>
public static class TestIdentity
{
    /// <summary>An identity with no User picked yet.</summary>
    public static IdentityService Create(TimeProvider? time = null)
    {
        var clock = time ?? TimeProvider.System;
        return new IdentityService(new FakeHttpClientFactory(new FakeTokenEndpoint(clock)), clock);
    }

    /// <summary>
    /// Signs <paramref name="identity"/> in as <paramref name="user"/>. The fake endpoint answers
    /// at once, so this completes synchronously; it suits bUnit's synchronous render helpers.
    /// </summary>
    public static void SignIn(IdentityService identity, UserDto user) =>
        identity.SignInAsync(user).GetAwaiter().GetResult();

    /// <summary>An identity signed in as <paramref name="user"/>, holding a token.</summary>
    public static async Task<IdentityService> SignedInAsync(UserDto user)
    {
        var identity = Create();
        await identity.SignInAsync(user);
        return identity;
    }
}

/// <summary>
/// Answers <c>POST /api/auth/token</c> with <c>token-1</c>, <c>token-2</c>, … valid for 15 minutes,
/// and 404 for <see cref="UnknownUserId"/>.
/// </summary>
public sealed class FakeTokenEndpoint(TimeProvider time) : HttpMessageHandler
{
    public const int UnknownUserId = 999;

    private int _calls;

    public int Calls => _calls;

    /// <summary>How long each call takes, so concurrent callers overlap.</summary>
    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var call = Interlocked.Increment(ref _calls);
        Assert.Equal("/api/auth/token", request.RequestUri?.AbsolutePath);
        var body = await request.Content!.ReadFromJsonAsync<TokenRequest>(
            TaskifyJson.Options,
            cancellationToken
        );

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (body!.UserId == UnknownUserId)
        {
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(
                new TokenResponse($"token-{call}", time.GetUtcNow().AddMinutes(15)),
                options: TaskifyJson.Options
            ),
        };
    }
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) =>
        new(handler, disposeHandler: false) { BaseAddress = new Uri("https://localhost/") };
}
