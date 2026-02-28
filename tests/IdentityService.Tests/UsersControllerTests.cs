using System.Security.Claims;

using FluentAssertions;

using Hotelier.Events;

using IdentityService.Domain;
using IdentityService.Infrastructure;
using IdentityService.Api;

using MassTransit;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Moq;

namespace IdentityService.Tests;

public class UsersControllerTests : IDisposable
{
    private readonly IdentityDbContext _db;
    private readonly Mock<IPublishEndpoint> _publisherMock;
    private readonly Mock<IReservationServiceClient> _reservationClientMock;
    private readonly UsersController _sut;
    private readonly User _seedUser;

    public UsersControllerTests()
    {
        _db = DbContextFactory.Create();
        _publisherMock = new Mock<IPublishEndpoint>();
        _reservationClientMock = new Mock<IReservationServiceClient>();
        var logger = new Mock<ILogger<UsersController>>();

        // Default: allow deletion
        _reservationClientMock
            .Setup(c => c.CanDeleteUserAsync(It.IsAny<Guid>(), It.IsAny<string>()))
            .ReturnsAsync((true, (string?)null));

        _seedUser = DbContextFactory.SeedUser(_db, "testuser", "Pass123!", UserType.Guest);

        _sut = new UsersController(_db, _publisherMock.Object, _reservationClientMock.Object, logger.Object);
        SetAuthenticatedUser(_seedUser.Id);
    }

    public void Dispose() => _db.Dispose();

    private void SetAuthenticatedUser(Guid userId)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    // ============================================================
    // GET /api/users/me
    // ============================================================

    [Fact]
    public async Task GetMyProfile_ReturnsCurrentUserProfile()
    {
        var result = await _sut.GetMyProfile();

        result.Should().BeOfType<OkObjectResult>();
        var profile = ((OkObjectResult)result).Value as UserProfile;
        profile.Should().NotBeNull();
        profile!.Username.Should().Be("testuser");
        profile.UserType.Should().Be(UserType.Guest);
    }

    [Fact]
    public async Task GetMyProfile_InvalidUser_ReturnsNotFound()
    {
        SetAuthenticatedUser(Guid.NewGuid()); // non-existent

        var result = await _sut.GetMyProfile();

        result.Should().BeOfType<NotFoundResult>();
    }

    // ============================================================
    // PUT /api/users/me
    // ============================================================

    [Fact]
    public async Task UpdateProfile_UpdatesName()
    {
        var request = new UpdateProfileRequest { Name = "NewName" };

        var result = await _sut.UpdateProfile(request);

        result.Should().BeOfType<OkObjectResult>();
        var profile = ((OkObjectResult)result).Value as UserProfile;
        profile!.Name.Should().Be("NewName");

        // Verify persisted
        var dbUser = _db.Users.Find(_seedUser.Id);
        dbUser!.Name.Should().Be("NewName");
    }

    [Fact]
    public async Task UpdateProfile_UpdatesEmail()
    {
        var request = new UpdateProfileRequest { Email = "new@test.com" };

        var result = await _sut.UpdateProfile(request);

        result.Should().BeOfType<OkObjectResult>();
        var profile = ((OkObjectResult)result).Value as UserProfile;
        profile!.Email.Should().Be("new@test.com");
    }

    [Fact]
    public async Task UpdateProfile_DuplicateEmail_ReturnsConflict()
    {
        DbContextFactory.SeedUser(_db, "other", "Pass123!");

        var request = new UpdateProfileRequest { Email = "other@test.com" };

        var result = await _sut.UpdateProfile(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task UpdateProfile_PublishesUserUpdatedEvent()
    {
        var request = new UpdateProfileRequest { Name = "Updated" };

        await _sut.UpdateProfile(request);

        _publisherMock.Verify(p => p.Publish(
            It.Is<UserUpdated>(e => e.UserId == _seedUser.Id),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // PUT /api/users/me/credentials
    // ============================================================

    [Fact]
    public async Task UpdateCredentials_ChangesPassword()
    {
        var request = new UpdateCredentialsRequest
        {
            CurrentPassword = "Pass123!",
            NewPassword = "NewPass456!"
        };

        var result = await _sut.UpdateCredentials(request);

        result.Should().BeOfType<OkObjectResult>();

        var dbUser = _db.Users.Find(_seedUser.Id);
        BCrypt.Net.BCrypt.Verify("NewPass456!", dbUser!.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCredentials_WrongCurrentPassword_ReturnsBadRequest()
    {
        var request = new UpdateCredentialsRequest
        {
            CurrentPassword = "WrongPass!",
            NewPassword = "NewPass456!"
        };

        var result = await _sut.UpdateCredentials(request);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCredentials_ChangesUsername()
    {
        var request = new UpdateCredentialsRequest
        {
            CurrentPassword = "Pass123!",
            Username = "newusername"
        };

        var result = await _sut.UpdateCredentials(request);

        result.Should().BeOfType<OkObjectResult>();
        var profile = ((OkObjectResult)result).Value as UserProfile;
        profile!.Username.Should().Be("newusername");
    }

    [Fact]
    public async Task UpdateCredentials_DuplicateUsername_ReturnsConflict()
    {
        DbContextFactory.SeedUser(_db, "taken2", "Pass123!");

        var request = new UpdateCredentialsRequest
        {
            CurrentPassword = "Pass123!",
            Username = "taken2"
        };

        var result = await _sut.UpdateCredentials(request);

        result.Should().BeOfType<ConflictObjectResult>();
    }

    // ============================================================
    // DELETE /api/users/me
    // ============================================================

    [Fact]
    public async Task DeleteAccount_RemovesUserFromDb()
    {
        var result = await _sut.DeleteAccount();

        result.Should().BeOfType<NoContentResult>();
        _db.Users.Find(_seedUser.Id).Should().BeNull();
    }

    [Fact]
    public async Task DeleteAccount_PublishesUserDeletedEvent()
    {
        await _sut.DeleteAccount();

        _publisherMock.Verify(p => p.Publish(
            It.Is<UserDeleted>(e =>
                e.UserId == _seedUser.Id &&
                e.UserType == nameof(UserType.Guest)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAccount_GuestWithActiveReservations_ReturnsConflict()
    {
        _reservationClientMock
            .Setup(c => c.CanDeleteUserAsync(_seedUser.Id, nameof(UserType.Guest)))
            .ReturnsAsync((false, "Cannot delete account: you have 1 active reservation(s)."));

        var result = await _sut.DeleteAccount();

        result.Should().BeOfType<ConflictObjectResult>();
        _db.Users.Find(_seedUser.Id).Should().NotBeNull("user should NOT be deleted");
    }

    [Fact]
    public async Task DeleteAccount_HostWithFutureReservations_ReturnsConflict()
    {
        // Replace seed user with a Host
        var host = DbContextFactory.SeedUser(_db, "hostuser", "Pass123!", UserType.Host);
        SetAuthenticatedUser(host.Id);

        _reservationClientMock
            .Setup(c => c.CanDeleteUserAsync(host.Id, nameof(UserType.Host)))
            .ReturnsAsync((false, "Cannot delete account: you have 2 active or pending reservation(s) on your accommodations."));

        var result = await _sut.DeleteAccount();

        result.Should().BeOfType<ConflictObjectResult>();
        _db.Users.Find(host.Id).Should().NotBeNull("host should NOT be deleted");
    }

    [Fact]
    public async Task DeleteAccount_HostWithNoFutureReservations_DeletesAndPublishes()
    {
        var host = DbContextFactory.SeedUser(_db, "hostclean", "Pass123!", UserType.Host);
        SetAuthenticatedUser(host.Id);

        // Default mock already returns canDelete = true

        var result = await _sut.DeleteAccount();

        result.Should().BeOfType<NoContentResult>();
        _db.Users.Find(host.Id).Should().BeNull();

        _publisherMock.Verify(p => p.Publish(
            It.Is<UserDeleted>(e =>
                e.UserId == host.Id &&
                e.UserType == nameof(UserType.Host)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ============================================================
    // GET /api/users/{id}
    // ============================================================

    [Fact]
    public async Task GetById_ExistingUser_ReturnsProfile()
    {
        var result = await _sut.GetById(_seedUser.Id);

        result.Should().BeOfType<OkObjectResult>();
        var profile = ((OkObjectResult)result).Value as UserProfile;
        profile!.Id.Should().Be(_seedUser.Id);
    }

    [Fact]
    public async Task GetById_NonExistentUser_ReturnsNotFound()
    {
        var result = await _sut.GetById(Guid.NewGuid());

        result.Should().BeOfType<NotFoundResult>();
    }
}
