namespace SyntaxCircus.Common.Tests;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params Claim[] claims)
        => new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void GetSubject_SubClaimPresent_ReturnsSubValue()
    {
        var user = PrincipalWith(new Claim("sub", "sub-value"), new Claim(ClaimTypes.NameIdentifier, "nameid-value"));

        user.GetSubject().ShouldBe("sub-value");
    }

    [Fact]
    public void GetSubject_OnlyNameIdentifierClaim_FallsBackToNameIdentifier()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.NameIdentifier, "nameid-value"));

        user.GetSubject().ShouldBe("nameid-value");
    }

    [Fact]
    public void GetSubject_NeitherClaimPresent_ReturnsNull()
    {
        var user = PrincipalWith();

        user.GetSubject().ShouldBeNull();
    }

    [Fact]
    public void GetEmail_EmailClaimPresent_ReturnsEmailValue()
    {
        var user = PrincipalWith(new Claim("email", "email-value"), new Claim(ClaimTypes.Email, "claimtype-email"));

        user.GetEmail().ShouldBe("email-value");
    }

    [Fact]
    public void GetEmail_OnlyClaimTypesEmail_FallsBackToClaimTypesEmail()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.Email, "claimtype-email"));

        user.GetEmail().ShouldBe("claimtype-email");
    }

    [Fact]
    public void GetEmail_NeitherClaimPresent_ReturnsNull()
    {
        var user = PrincipalWith();

        user.GetEmail().ShouldBeNull();
    }

    [Fact]
    public void GetDisplayName_NameClaimPresent_ReturnsNameValue()
    {
        var user = PrincipalWith(new Claim("name", "name-value"), new Claim("preferred_username", "preferred-value"));

        user.GetDisplayName().ShouldBe("name-value");
    }

    [Fact]
    public void GetDisplayName_OnlyPreferredUsername_FallsBackToPreferredUsername()
    {
        var user = PrincipalWith(new Claim("preferred_username", "preferred-value"));

        user.GetDisplayName().ShouldBe("preferred-value");
    }

    [Fact]
    public void GetDisplayName_NeitherClaimPresent_ReturnsNull()
    {
        var user = PrincipalWith();

        user.GetDisplayName().ShouldBeNull();
    }
}
