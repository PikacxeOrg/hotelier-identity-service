using FluentAssertions;

using IdentityService.Domain;
using IdentityService.Infrastructure;
using IdentityService.Api;
using IdentityService.Tests;

using MassTransit;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace IdentityService.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IJwtTokenService> _jwtMock;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _db = DbContextFactory.Create();
        _jwtMock = new Mock<IJwtTokenService>();
        _publisherMock = new Mock<IPublishEndpoint>();
        var logger = new Mock<ILogger<AuthController>>();

        // Default JWT mock behaviour
        _jwtMock.Setup(j => j.GenerateAccessToken(It.IsAny<User>()))
            .Returns("test-access-token");
        _jwtMock.Setup(j => j.GenerateRefreshToken(It.IsAny<User>()))
            .Returns((User u) => new RefreshToken
            {
                UserId = u.Id,
                Token = "test-refresh-token",
                ExpiresAt = DateTime.UtcNow.AddDays(7)
            });

        _sut = new AuthController(_db, _jwtMock.Object, _publisherMock.Object, logger.Object);
    }

    public void Dispose() => _db.Dispose();

    // ============================================================
    // Register
    // ============================================================

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        var request = new RegisterRequest
        {
            Username = "newuser",
            Password = "Pass123!",
            Name = "John",
            LastName = "Doe",
            Email = "john@test.com",
            Address = "123 Main St",
            UserType = UserType.Guest
        };

        var result = await _sut.Register(request);

        result.Should().BeOfType<CreatedAtActionResult>();
        var created = (CreatedAtActionResult)result;
        var response = created.Value as AuthResponse;
        response.Should().NotBeNull();
        response!.AccessToken.Should().Be("test-access-token");
        response.User.Username.Should().Be("newuser");
    }

    [Fact]
    public async Task Register_CreatesUserInDatabase()
    {
        var request = new RegisterRequest
        {
            Username = "dbuser",
            Password = "Pass123!",
            Name = "Jane",
            LastName = "Doe",
            Email = "jane@test.com",
            Address = "456 Oak St",
            UserType = UserType.Host
        };

        await _sut.Register(request);

        var user = _db.Users.FirstOrDefault(u => u.Username == "dbuser");
        user.Should().NotBeNull();
        user!.UserType.Should().Be(UserType.Host);
        BCrypt.Net.BCrypt.Verify("Pass123!", user.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task Register_PublishesUserRegisteredEvent()
    {
        var request = new RegisterRequest
        {
            Username = "eventuser",
            Password = "Pass123!",
            Name = "Event",
            LastName = "User",
            Email = "event@test.com",
            Address = "789 Pine St",
            UserType = UserType.Guest
        };

        await _sut.Register(request);

        _publisherMock.Verify(p => p.Publish(
            It.Is<UserRegistered>(e =>
                e.Username == "eventuser" &&
                e.UserType == UserType.Guest),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsConflict()
    {
        DbContextFactory.SeedUser(_db, "taken");

        var request = new RegisterRequest
        {
            Username = "taken",
            Password = "Pass123!",
            Name = "Dup",
            LastName = "User",
            Email = "dup@test.com",
            Address = "1 Dup St",
            UserType = UserType.Guest
        };

        var result = await _sut.Register(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        DbContextFactory.SeedUser(_db, "user1");

        var request = new RegisterRequest
        {
            Username = "user2",
            Password = "Pass123!",
            Name = "Dup",
            LastName = "Email",
            Email = "user1@test.com", // same email as seeded user
            Address = "1 Dup St",
            UserType = UserType.Guest
        };

        var result = await _sut.Register(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ============================================================
    // Login
    // ============================================================

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        DbContextFactory.SeedUser(_db, "loginuser", "Secret123!");

        var request = new LoginRequest { Username = "loginuser", Password = "Secret123!" };

        var result = await _sut.Login(request);

        result.Should().BeOfType<OkObjectResult>();
        var ok = (OkObjectResult)result;
        var response = ok.Value as AuthResponse;
        response.Should().NotBeNull();
        response!.AccessToken.Should().Be("test-access-token");
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        DbContextFactory.SeedUser(_db, "loginuser2", "Correct123!");

        var request = new LoginRequest { Username = "loginuser2", Password = "Wrong123!" };

        var result = await _sut.Login(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_NonexistentUser_ReturnsUnauthorized()
    {
        var request = new LoginRequest { Username = "nobody", Password = "Pass123!" };

        var result = await _sut.Login(request);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Login_CreatesRefreshTokenInDb()
    {
        var user = DbContextFactory.SeedUser(_db, "tokenuser", "Pass123!");

        await _sut.Login(new LoginRequest { Username = "tokenuser", Password = "Pass123!" });

        _db.RefreshTokens.Any(rt => rt.UserId == user.Id).Should().BeTrue();
    }
}
