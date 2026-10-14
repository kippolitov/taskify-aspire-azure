using Microsoft.IdentityModel.Tokens;

namespace Taskify.Api.Identity;

/// <summary>
/// Signs and checks the Active User's identity. Bound from the <c>Auth</c> section; the key
/// is a secret supplied per environment, never stored in the repository (FR-030).
/// </summary>
public sealed class AuthOptions
{
    public const string Section = "Auth";

    private const int MinimumKeyBytes = 32;

    /// <summary>At least 32 random bytes, base64-encoded.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(15);

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey));

    internal bool IsValid()
    {
        var buffer = new byte[SigningKey.Length];
        return Convert.TryFromBase64String(SigningKey, buffer, out var length)
            && length >= MinimumKeyBytes
            && !string.IsNullOrWhiteSpace(Issuer)
            && !string.IsNullOrWhiteSpace(Audience);
    }

    /// <summary>
    /// Signature, issuer, audience and lifetime, with the default clock skew. Lifetime is
    /// checked against <paramref name="time"/>, the same clock that sets <c>exp</c>.
    /// </summary>
    public TokenValidationParameters CreateValidationParameters(TimeProvider time) =>
        new()
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = CreateSigningKey(),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            LifetimeValidator = (notBefore, expires, _, parameters) =>
            {
                var now = time.GetUtcNow().UtcDateTime;
                return expires > now - parameters.ClockSkew
                    && (notBefore is null || notBefore <= now + parameters.ClockSkew);
            },
        };
}
