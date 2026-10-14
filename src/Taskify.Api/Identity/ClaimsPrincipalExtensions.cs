using System.Globalization;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Taskify.Api.Identity;

public static class ClaimsPrincipalExtensions
{
    extension(ClaimsPrincipal user)
    {
        /// <summary>The Active User's id, from the token's <c>sub</c> claim.</summary>
        public int UserId =>
            int.Parse(
                user.FindFirstValue(JwtRegisteredClaimNames.Sub)!,
                CultureInfo.InvariantCulture
            );
    }
}
