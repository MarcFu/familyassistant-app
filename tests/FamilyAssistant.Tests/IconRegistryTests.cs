using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

public class IconRegistryTests
{
    [Theory]
    [InlineData("Staubsauger", "Vacuum")]
    [InlineData("Saugroboter", "RobotVacuum")]
    [InlineData("Spülmaschine", "Dishwasher")]
    [InlineData("Waschmaschine", "WashingMachine")]
    [InlineData("Trockner", "TumbleDryer")]
    public void KeywordMapper_ReturnsCuratedHouseholdIcons(string choreName, string expectedIcon)
    {
        var icon = KeywordIconMapper.GetIcon(choreName);

        Assert.Equal(expectedIcon, icon);
        Assert.True(IconRegistry.Exists(icon!));
        Assert.False(string.IsNullOrWhiteSpace(IconRegistry.GetSvg(icon!)));
    }

    [Theory]
    [InlineData("staubsauger", "Vacuum")]
    [InlineData("saugroboter", "RobotVacuum")]
    [InlineData("spülmaschine", "Dishwasher")]
    public void SearchIndex_FindsCuratedHouseholdIcons(string query, string expectedIcon)
    {
        var results = IconSearchIndex.Search(query, 10);

        Assert.Contains(expectedIcon, results);
    }

    [Theory]
    [InlineData("Vacuum")]
    [InlineData("RobotVacuum")]
    [InlineData("Broom")]
    [InlineData("Dishwasher")]
    [InlineData("WashingMachine")]
    [InlineData("TumbleDryer")]
    public void CuratedIcons_ReturnMudBlazorSvgMarkup(string iconName)
    {
        var svg = IconRegistry.GetSvg(iconName);

        Assert.StartsWith("<path ", svg);
    }
}
