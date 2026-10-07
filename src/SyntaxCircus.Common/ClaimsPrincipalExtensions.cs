using System.Security.Claims;

namespace SyntaxCircus.Common;

public static class ClaimsPrincipalExtensions
{
    public static string? GetSubject(this ClaimsPrincipal user)
        => FindFirstValue(user, "sub") ?? FindFirstValue(user, ClaimTypes.NameIdentifier);

    public static string? GetEmail(this ClaimsPrincipal user)
        => FindFirstValue(user, "email") ?? FindFirstValue(user, ClaimTypes.Email);

    public static string? GetDisplayName(this ClaimsPrincipal user)
        => FindFirstValue(user, "name") ?? FindFirstValue(user, "preferred_username");

    private static string? FindFirstValue(ClaimsPrincipal user, string claimType)
        => user.FindFirst(claimType)?.Value;
}
