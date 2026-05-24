using FamilyAssist.Data;
using Microsoft.EntityFrameworkCore;

namespace FamilyAssist.Tests;

/// <summary>
/// Helper to create in-memory database instances for testing.
/// </summary>
public static class TestDbFactory
{
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        var db = new AppDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
