using System.Security.Claims;

namespace WebApplication2.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static int GetUserId(this ClaimsPrincipal user)
    {
        var claimValue = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("id");

        if (string.IsNullOrEmpty(claimValue) || !int.TryParse(claimValue, out int userId))
        {
            throw new UnauthorizedAccessException("User identification token is invalid or missing.");
        }

        return userId;
    }
}