using System.Globalization;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Taskify.Shared.Dtos;

namespace Taskify.Api.Identity;

/// <summary>Issues the HMAC-SHA256 signed identity for a User (FR-021, FR-022).</summary>
public sealed class TokenIssuer(IOptions<AuthOptions> options, TimeProvider time)
{
    private readonly JsonWebTokenHandler _handler = new();

    public TokenResponse Issue(int userId)
    {
        var auth = options.Value;
        var now = time.GetUtcNow();
        var expiresAt = now.Add(auth.Lifetime);

        var token = _handler.CreateToken(
            new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([
                    new Claim(
                        JwtRegisteredClaimNames.Sub,
                        userId.ToString(CultureInfo.InvariantCulture)
                    ),
                ]),
                Issuer = auth.Issuer,
                Audience = auth.Audience,
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = expiresAt.UtcDateTime,
                SigningCredentials = new SigningCredentials(
                    auth.CreateSigningKey(),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );

        return new TokenResponse(token, expiresAt);
    }
}
