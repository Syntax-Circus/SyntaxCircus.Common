using System.Security.Claims;

namespace SyntaxCircus.Common;

public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? Email { get; }

    string? DisplayName { get; }

    ClaimsPrincipal Principal { get; }
}
