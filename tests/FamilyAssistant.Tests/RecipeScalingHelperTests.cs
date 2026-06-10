using FamilyAssistant.Services;

namespace FamilyAssistant.Tests;

public class RecipeScalingHelperTests
{
    [Fact]
    public void ScaleQuantity_ScalesUpFromFourToSixServings()
    {
        var result = RecipeScalingHelper.ScaleQuantity(550m, 4m, 6m);

        Assert.Equal(825m, result);
    }

    [Fact]
    public void ScaleQuantity_LeavesValueUntouched_WhenBaseServingsInvalid()
    {
        var result = RecipeScalingHelper.ScaleQuantity(200m, 0m, 6m);

        Assert.Equal(200m, result);
    }

    [Theory]
    [InlineData(5, "5")]
    [InlineData(7.5, "7.5")]
    [InlineData(7.25, "7.25")]
    public void FormatQuantity_RemovesUnnecessaryTrailingZeros(decimal value, string expected)
    {
        Assert.Equal(expected, RecipeScalingHelper.FormatQuantity(value));
    }
}
