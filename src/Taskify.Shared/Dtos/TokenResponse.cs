namespace Taskify.Shared.Dtos;

/// <summary>An identity for the Active User. Renew it before <see cref="ExpiresAt"/>.</summary>
public record TokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
