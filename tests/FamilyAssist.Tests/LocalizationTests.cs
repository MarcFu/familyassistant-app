using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
// SharedResource marker class is in namespace FamilyAssist (root)

namespace FamilyAssist.Tests;

public class LocalizationTests
{
    private IStringLocalizer<SharedResource> CreateLocalizer()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var sp = services.BuildServiceProvider();
        var factory = sp.GetRequiredService<IStringLocalizerFactory>();
        return new StringLocalizer<SharedResource>(factory);
    }

    [Fact]
    public void German_Common_Save_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("de");
        var localizer = CreateLocalizer();

        var result = localizer["Common.Save"];

        Assert.False(result.ResourceNotFound, $"Resource 'Common.Save' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.Equal("Speichern", result.Value);
    }

    [Fact]
    public void English_Common_Save_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en");
        var localizer = CreateLocalizer();

        var result = localizer["Common.Save"];

        Assert.False(result.ResourceNotFound, $"Resource 'Common.Save' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.Equal("Save", result.Value);
    }

    [Fact]
    public void German_TasksTitle_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("de");
        var localizer = CreateLocalizer();

        var result = localizer["Tasks.Title"];

        Assert.False(result.ResourceNotFound, $"Resource 'Tasks.Title' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.NotEqual("Tasks.Title", result.Value);
    }

    [Fact]
    public void UnknownKey_ReturnsKeyAsValue()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("de");
        var localizer = CreateLocalizer();

        var result = localizer["NonExistent.Key.That.Does.Not.Exist"];

        Assert.True(result.ResourceNotFound);
        Assert.Equal("NonExistent.Key.That.Does.Not.Exist", result.Value);
    }

    [Fact]
    public void French_Common_Save_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("fr");
        var localizer = CreateLocalizer();

        var result = localizer["Common.Save"];

        Assert.False(result.ResourceNotFound, $"Resource 'Common.Save' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.Equal("Enregistrer", result.Value);
    }

    [Fact]
    public void French_TasksTitle_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("fr");
        var localizer = CreateLocalizer();

        var result = localizer["Tasks.Title"];

        Assert.False(result.ResourceNotFound, $"Resource 'Tasks.Title' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.Equal("Tâches", result.Value);
    }

    [Fact]
    public void French_Nav_Settings_ReturnsTranslation()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("fr");
        var localizer = CreateLocalizer();

        var result = localizer["Nav.Settings"];

        Assert.False(result.ResourceNotFound, $"Resource 'Nav.Settings' not found. SearchedLocation: {result.SearchedLocation}");
        Assert.Equal("Paramètres", result.Value);
    }
}
