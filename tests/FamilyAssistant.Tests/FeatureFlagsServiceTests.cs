using FamilyAssistant.Models;
using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

public class FeatureFlagsServiceTests
{
    [Fact]
    public async Task IsEnabledAsync_ReturnsDefaultFalse_WhenSettingMissing()
    {
        using var db = TestDbFactory.Create();
        var service = new FeatureFlagsService(db);

        var enabled = await service.IsRecipesEnabledAsync();

        Assert.False(enabled);
    }

    [Fact]
    public async Task SetEnabledAsync_PersistsBooleanValue()
    {
        using var db = TestDbFactory.Create();
        var service = new FeatureFlagsService(db);

        await service.SetRecipesEnabledAsync(true);

        Assert.True(await service.IsRecipesEnabledAsync());
        Assert.Equal("True", db.AppSettings.Single(s => s.Key == FeatureFlagKeys.RecipesEnabled).Value);
    }

    [Fact]
    public async Task IsEnabledAsync_FallsBackToDefault_WhenValueInvalid()
    {
        using var db = TestDbFactory.Create();
        db.AppSettings.Add(new AppSetting
        {
            Key = FeatureFlagKeys.RecipesEnabled,
            Value = "maybe"
        });
        await db.SaveChangesAsync();

        var service = new FeatureFlagsService(db);

        Assert.True(await service.IsEnabledAsync(FeatureFlagKeys.RecipesEnabled, defaultValue: true));
    }
}
