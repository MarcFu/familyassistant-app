using System.Globalization;

namespace FamilyAssistant.Services;

public static class RecipeScalingHelper
{
    public static decimal ScaleQuantity(decimal quantity, decimal baseServings, decimal targetServings)
    {
        if (baseServings <= 0 || targetServings <= 0)
        {
            return quantity;
        }

        return Math.Round(quantity / baseServings * targetServings, 2, MidpointRounding.AwayFromZero);
    }

    public static string FormatQuantity(decimal quantity)
    {
        return decimal.Truncate(quantity) == quantity
            ? quantity.ToString("0", CultureInfo.InvariantCulture)
            : quantity.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
