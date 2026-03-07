using IdentityService.Domain;
using IdentityService.Infrastructure;

using Microsoft.EntityFrameworkCore;

namespace IdentityService.Tests;

/// <summary>
/// Creates a fresh in-memory IdentityDbContext for each test.
/// </summary>
public static class DbContextFactory
{
    public static IdentityDbContext Create(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();

        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var context = new IdentityDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    /// <summary>
    /// Seeds a test user and returns it.
    /// </summary>
    public static User SeedUser(
        IdentityDbContext db,
        string username = "testuser",
        string password = "Test123!",
        UserType userType = UserType.Guest)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Name = "Test",
            LastName = "User",
            Email = $"{username}@test.com",
            Address = "123 Test St",
            UserType = userType
        };

        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }
}
