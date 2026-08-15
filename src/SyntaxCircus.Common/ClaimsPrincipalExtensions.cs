using System.Security.Claims;

namespace SyntaxCircus.Common;

public static class ClaimsPrincipalExtensions
{
    public static string? GetSubject(this ClaimsPrincipal user)
        => user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetEmail(this ClaimsPrincipal user)
        => user.FindFirstValue("email") ?? user.FindFirstValue(ClaimTypes.Email);

    public static string? GetDisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue("name") ?? user.FindFirstValue("preferred_username");
}
