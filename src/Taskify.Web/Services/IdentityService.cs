using System.Net.Http.Json;
using Taskify.Shared;
using Taskify.Shared.Dtos;
using Taskify.Shared.Requests;

namespace Taskify.Web.Services;

/// <summary>
/// The Active User for this Blazor circuit, and the server-issued identity that vouches for
/// them. The identity is renewed silently when less than two minutes remain (FR-029).
/// State is lost when the circuit ends.
/// </summary>
public sealed class IdentityService(IHttpClientFactory httpClientFactory, TimeProvider time)
    : IDisposable
{
    /// <summary>The named HTTP client that calls the token endpoint.</summary>
    public const string ClientName = "auth";

    private static readonly TimeSpan RenewalWindow = TimeSpan.FromMinutes(2);

    private readonly SemaphoreSlim _renewal = new(1, 1);
    private TokenResponse? _token;

    public UserDto? CurrentUser
    {
        get;
        private set
        {
            field = value;
            OnChange?.Invoke();
        }
    }

    public bool IsAuthenticated => CurrentUser is not null;

    public event Action? OnChange;

    /// <summary>Asks the server for an identity for <paramref name="user"/>, then acts as them.</summary>
    public async Task SignInAsync(UserDto user, CancellationToken ct = default)
    {
        _token = await RequestTokenAsync(user.Id, ct);
        CurrentUser = user;
    }

    /// <summary>The current token, renewed first when it is about to expire; null when signed out.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (_token is not { } token || !NeedsRenewal(token))
        {
            return _token?.AccessToken;
        }

        await _renewal.WaitAsync();
        try
        {
            if (_token is not null && CurrentUser is { } user && NeedsRenewal(_token))
            {
                _token = await RequestTokenAsync(user.Id, CancellationToken.None);
            }

            return _token?.AccessToken;
        }
        finally
        {
            _renewal.Release();
        }
    }

    public void ClearUser()
    {
        _token = null;
        CurrentUser = null;
    }

    public void Dispose() => _renewal.Dispose();

    private bool NeedsRenewal(TokenResponse token) =>
        token.ExpiresAt - time.GetUtcNow() < RenewalWindow;

    private async Task<TokenResponse> RequestTokenAsync(int userId, CancellationToken ct)
    {
        using var client = httpClientFactory.CreateClient(ClientName);
        var response = await client.PostAsJsonAsync(
            "api/auth/token",
            new TokenRequest(userId),
            TaskifyJson.Options,
            ct
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TokenResponse>(TaskifyJson.Options, ct))!;
    }
}
