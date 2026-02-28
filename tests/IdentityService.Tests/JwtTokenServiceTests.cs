using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

using FluentAssertions;

using IdentityService.Domain;
using IdentityService.Infrastructure;

using Microsoft.Extensions.Configuration;

namespace IdentityService.Tests;

public class JwtTokenServiceTests
{
    private readonly IConfiguration _config;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "super-secret-dev-key-change-me-in-prod-32chars!",
            ["Jwt:Issuer"] = "hotelier-identity",
            ["Jwt:Audience"] = "hotelier",
            ["Jwt:AccessTokenExpirationMinutes"] = "60",
            ["Jwt:RefreshTokenExpirationDays"] = "7"
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        _sut = new JwtTokenService(_config);
    }

    private static User CreateTestUser() => new()
    {
        Id = Guid.NewGuid(),
        Username = "testuser",
        Email = "test@test.com",
        UserType = UserType.Guest
    };

    // ----- GenerateAccessToken -----

    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        var user = CreateTestUser();

        var token = _sut.GenerateAccessToken(user);

        token.Should().NotBeNullOrWhiteSpace();
        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_ContainsExpectedClaims()
    {
        var user = CreateTestUser();

        var token = _sut.GenerateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        jwt.Subject.Should().Be(user.Id.ToString());
        jwt.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.UniqueName && c.Value == user.Username);
        jwt.Claims.Should().Contain(c =>
            c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwt.Issuer.Should().Be("hotelier-identity");
        jwt.Audiences.Should().Contain("hotelier");
    }

    [Fact]
    public void GenerateAccessToken_ExpiresInConfiguredMinutes()
    {
        var user = CreateTestUser();
        var before = DateTime.UtcNow;

        var token = _sut.GenerateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.ValidTo.Should().BeAfter(before.AddMinutes(59));
        jwt.ValidTo.Should().BeBefore(before.AddMinutes(61));
    }

    [Theory]
    [InlineData(UserType.Guest)]
    [InlineData(UserType.Host)]
    public void GenerateAccessToken_IncludesCorrectRole(UserType userType)
    {
        var user = CreateTestUser();
        user.UserType = userType;

        var token = _sut.GenerateAccessToken(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        jwt.Claims.Should().Contain(c =>
            c.Type == ClaimTypes.Role && c.Value == userType.ToString());
    }

    // ----- GenerateRefreshToken -----

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyToken()
    {
        var user = CreateTestUser();

        var rt = _sut.GenerateRefreshToken(user);

        rt.Token.Should().NotBeNullOrWhiteSpace();
        rt.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void GenerateRefreshToken_ExpiresInConfiguredDays()
    {
        var user = CreateTestUser();
        var before = DateTime.UtcNow;

        var rt = _sut.GenerateRefreshToken(user);

        rt.ExpiresAt.Should().BeAfter(before.AddDays(6));
        rt.ExpiresAt.Should().BeBefore(before.AddDays(8));
    }

    [Fact]
    public void GenerateRefreshToken_ProducesUniqueTokens()
    {
        var user = CreateTestUser();

        var rt1 = _sut.GenerateRefreshToken(user);
        var rt2 = _sut.GenerateRefreshToken(user);

        rt1.Token.Should().NotBe(rt2.Token);
    }

    // ----- ValidateExpiredToken -----

    [Fact]
    public void ValidateExpiredToken_AcceptsValidToken()
    {
        var user = CreateTestUser();
        var token = _sut.GenerateAccessToken(user);

        var principal = _sut.ValidateExpiredToken(token);

        principal.Should().NotBeNull();
        principal!.FindFirstValue(ClaimTypes.NameIdentifier)
            .Should().Be(user.Id.ToString());
    }

    [Fact]
    public void ValidateExpiredToken_ReturnsNullForGarbageToken()
    {
        var result = _sut.ValidateExpiredToken("not.a.valid.jwt");

        result.Should().BeNull();
    }
}
